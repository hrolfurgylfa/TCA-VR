# Heavily inspired by the pyopenxr color_cube.py example: https://github.com/cmbruns/pyopenxr_examples/blob/1df4390dc16c1fe553e0e703f6ad2b52f7f85dce/xr_examples/color_cube.py

from dataclasses import dataclass, field
import inspect

import numpy as np
from OpenGL import GL
from OpenGL.GL.shaders import compileShader, compileProgram
import xr
import mss

from data_sender import DataSender
from timer import Timer

UP = np.array([1, 0, 0])


def normalized(vector: np.ndarray) -> np.ndarray:
    length = np.linalg.norm(vector)
    return vector if length == 0 else vector / length


@dataclass
class OrthographicView:
    left: float = -1.0
    right: float = 1.0
    bottom: float = -1.0
    top: float = 1.0
    near_plane: float = -100.0
    far_plane: float = 100.0

    def get_matrix(self) -> list[float]:
        n, f = (self.near_plane, self.far_plane)
        t, b = (self.top, self.bottom)
        l, r = (self.left, self.right)
        # fmt: off
        return [
                2.0 / (r - l), 0.0,           0.0,          -(r + l) / (r - l),
                0.0          , 2.0 / (t - b), 0.0,          -(t + b) / (t - b),
                0.0          , 0.0,           2.0 / (n - f), (n + f) / (n - f),
                0.0          , 0.0,           0.0,           1.0]
        # fmt: on


@dataclass
class ViewMatrix:
    eye: np.ndarray = field(
        default_factory=lambda: np.array((0, 0, 0), dtype="float32")
    )
    u: np.ndarray = field(default_factory=lambda: np.array((1, 0, 0), dtype="float32"))
    v: np.ndarray = field(default_factory=lambda: np.array((0, 1, 0), dtype="float32"))
    n: np.ndarray = field(default_factory=lambda: np.array((0, 0, 1), dtype="float32"))

    def _calc_u_v(self, up: np.ndarray) -> None:
        self.u = normalized(np.cross(up, self.n))
        self.v = np.cross(self.n, self.u)

    def look_at(self, target: np.ndarray, up: np.ndarray) -> None:
        self.n = normalized(self.eye - target)
        self._calc_u_v(up)

    def look_direction(self, normalized_direction: np.ndarray, up: np.ndarray) -> None:
        self.n = normalized_direction
        self._calc_u_v(up)

    def get_matrix(self) -> list[float]:
        # fmt: off
        return [
            self.u[0], self.u[1], self.u[2], (-self.eye).dot(self.u),
            self.v[0], self.v[1], self.v[2], (-self.eye).dot(self.v),
            self.n[0], self.n[1], self.n[2], (-self.eye).dot(self.n),
            0.0,      0.0,      0.0,      1.0]
        # fmt: on


@dataclass
class EyeData:
    pos: np.ndarray = field(
        default_factory=lambda: np.array((0, 0, 0), dtype="float32")
    )
    quaternion: np.ndarray = field(
        default_factory=lambda: np.array((0, 0, 0, 0), dtype="float32")
    )
    fov: np.ndarray = field(
        default_factory=lambda: np.array((0, 0, 0, 0), dtype="float32")
    )

    def to_bytes(self) -> bytes:
        return self.pos.tobytes() + self.quaternion.tobytes() + self.fov.tobytes()

    @classmethod
    def from_openxr(cls, view) -> "EyeData":
        self = cls()
        self.load_openxr(view)
        return self
    
    def load_openxr(self, view) -> None:
        self.pos[0] = view.pose.position.x
        self.pos[1] = view.pose.position.y
        self.pos[2] = view.pose.position.z
        self.quaternion[0] = view.pose.orientation.w
        self.quaternion[1] = view.pose.orientation.x
        self.quaternion[2] = view.pose.orientation.y
        self.quaternion[3] = view.pose.orientation.z
        self.fov[0] = view.fov.angle_up
        self.fov[1] = view.fov.angle_down
        self.fov[2] = view.fov.angle_right
        self.fov[3] = view.fov.angle_left

@dataclass
class HeadsetData:
    left_eye: EyeData = field(default_factory=lambda: EyeData())
    right_eye: EyeData = field(default_factory=lambda: EyeData())

    def to_bytes(self) -> bytes:
        return self.left_eye.to_bytes() + self.right_eye.to_bytes()


# ContextObject is a high level pythonic class meant to keep simple cases simple.
with xr.ContextObject(
    instance_create_info=xr.InstanceCreateInfo(
        application_info=xr.ApplicationInfo(
            application_name="TCA-VR",
            application_version=xr.Version(0, 0, 1),
        ),
        enabled_extension_names=[
            # A graphics extension is mandatory (without a headless extension)
            xr.KHR_OPENGL_ENABLE_EXTENSION_NAME,
        ],
    ),
) as context:
    vertex_shader = compileShader(
        inspect.cleandoc(
            """
        #version 430

        layout(location = 0) uniform mat4 u_projection_matrix = mat4(1);
        layout(location = 1) uniform mat4 u_view_matrix = mat4(1);
        layout(location = 4) uniform float u_eye_num = 0.0;
        layout(location = 5) uniform float u_capture_aspect_ratio = 16/9;

        const vec2 POSITIONS[4] = vec2[4](
            vec2(-1.0, -1.0), // 4: lower left front
            vec2(+1.0, -1.0), // 5: lower right front
            vec2(-1.0, +1.0), // 6: upper left front
            vec2(+1.0, +1.0)  // 7: upper right front
        );
        const vec2 UVS[4] = vec2[4](
            vec2(+0.0, +1.0),
            vec2(+0.0, +0.0),
            vec2(+1.0, +1.0),
            vec2(+1.0, +0.0)
        );
        const int CUBE_INDICES[6] = int[6](0, 1, 2, 2, 1, 3);

        out vec2 v_uv;

        void main() {
            int vertexIndex = CUBE_INDICES[gl_VertexID];
            int normalIndex = gl_VertexID / 6;

            // Calculate the UVs for the current eye
            v_uv = UVS[vertexIndex];
            v_uv.x = min(1.0, v_uv.x + 0.5 * u_eye_num); // Move to the middle if eye is 1
            v_uv.x /= (1 - u_eye_num) + 1; // Divide in half if eye is 0

            // Set the position of the current vertex
            vec4 world_pos = vec4(POSITIONS[vertexIndex] * 1.5, 1.0, 1.0);
            gl_Position = u_projection_matrix * u_view_matrix * world_pos;
        }
        """
        ),
        GL.GL_VERTEX_SHADER,
    )
    fragment_shader = compileShader(
        inspect.cleandoc(
            """
        #version 430
        uniform sampler2D u_tex;
        
        in vec2 v_uv;
        out vec4 FragColor;

        void main() {
            vec4 color = texture2D(u_tex, v_uv);
            FragColor = vec4(color.xyz, 1.0);
        }
        """
        ),
        GL.GL_FRAGMENT_SHADER,
    )
    shader = compileProgram(vertex_shader, fragment_shader)

    vao = GL.glGenVertexArrays(1)
    GL.glBindVertexArray(vao)
    GL.glEnable(GL.GL_DEPTH_TEST)  # Not sure if this needs to be inside
    GL.glBindVertexArray(0)
    GL.glClearColor(0.2, 0.2, 0.2, 1)
    GL.glClearDepth(1.0)

    # Initialize the texture with no data
    monitor = {"top": 0, "left": 0, "width": 1920, "height": 1080}
    tex_id = GL.glGenTextures(1)
    GL.glBindTexture(GL.GL_TEXTURE_2D, tex_id)
    GL.glTexParameteri(GL.GL_TEXTURE_2D, GL.GL_TEXTURE_MAG_FILTER, GL.GL_LINEAR)
    GL.glTexParameteri(GL.GL_TEXTURE_2D, GL.GL_TEXTURE_MIN_FILTER, GL.GL_LINEAR)
    GL.glTexParameteri(GL.GL_TEXTURE_2D, GL.GL_TEXTURE_WRAP_S, GL.GL_REPEAT)
    GL.glTexParameteri(GL.GL_TEXTURE_2D, GL.GL_TEXTURE_WRAP_T, GL.GL_REPEAT)
    GL.glTexImage2D(
        GL.GL_TEXTURE_2D,
        0,
        GL.GL_RGBA,
        monitor["width"],
        monitor["height"],
        0,
        GL.GL_RGBA,
        GL.GL_UNSIGNED_BYTE,
        None,
    )
    GL.glBindTexture(GL.GL_TEXTURE_2D, 0)

    # Create the View and Projection Matrices
    projection_matrix = OrthographicView(near_plane=0.05, far_plane=100.0).get_matrix()
    view_m = ViewMatrix()
    view_m.look_direction(np.array([0, 0, -1]), UP)
    view_matrix = view_m.get_matrix()

    headset_data = HeadsetData()

    with DataSender() as sender:
        with mss.mss() as sct:
            print("Starting XR Server")
            for frame_index, frame_state in enumerate(context.frame_loop()):

                # Load the screenshot to OpenGL
                GL.glBindTexture(GL.GL_TEXTURE_2D, tex_id)
                screenshot = sct.grab(monitor)
                GL.glTexSubImage2D(
                    GL.GL_TEXTURE_2D,
                    0,
                    0,
                    0,
                    monitor["width"],
                    monitor["height"],
                    GL.GL_BGRA,
                    GL.GL_UNSIGNED_BYTE,
                    screenshot.raw,
                )
                GL.glBindTexture(GL.GL_TEXTURE_2D, 0)

                # Render both eyes
                for view_index, view in enumerate(context.view_loop(frame_state)):
                    # Prepare OpenGL
                    GL.glClear(GL.GL_COLOR_BUFFER_BIT | GL.GL_DEPTH_BUFFER_BIT)
                    GL.glUseProgram(shader)

                    # Load Shader Variables
                    GL.glUniformMatrix4fv(0, 1, False, projection_matrix)
                    GL.glUniformMatrix4fv(1, 1, False, view_matrix)
                    GL.glUniform1f(4, float(view_index))
                    GL.glUniform1f(5, monitor["width"] / monitor["height"])

                    # Do the render
                    GL.glBindVertexArray(vao)
                    GL.glBindTexture(GL.GL_TEXTURE_2D, tex_id)
                    GL.glDrawArrays(GL.GL_TRIANGLES, 0, 6)
                    GL.glBindTexture(GL.GL_TEXTURE_2D, 0)
                    GL.glBindVertexArray(0)

                    # Gather up information for the current eye
                    if view_index == 0:
                        headset_data.left_eye.load_openxr(view)
                    elif view_index == 1:
                        headset_data.right_eye.load_openxr(view)

                # Send the information we have gathered for all the eyes
                sender.write(headset_data.to_bytes())
