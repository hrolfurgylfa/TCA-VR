// Adapted from: https://github.com/cmbruns/pyopenxr/blob/main/src/xr/matrix4x4f.py

// Copyright (c) 2017 The Khronos Group Inc.
// Copyright (c) 2016 Oculus VR, LLC.
//
// SPDX-License-Identifier: Apache-2.0
//
// Licensed under the Apache License, Version 2.0 (the "License")
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// Author: J.M.P. van Waveren

using UnityEngine;

namespace TCA_VR.Extras;

static class ProjectionMatrixExtras
{
    public static Matrix4x4 CreateProjection(float tan_angle_left, float tan_angle_right, float tan_angle_up, float tan_angle_down, float near_z, float far_z)
    {
        var isOpenGL = SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.OpenGLCore
            || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.OpenGLES2
            || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3;
        var isVulcan = SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Vulkan;

        var tan_angle_width = tan_angle_right - tan_angle_left;
        // Set to tan_angle_down - tan_angle_up for a clip space with positive Y down (Vulkan).
        // Set to tan_angle_up - tan_angle_down for a clip space with positive Y up (OpenGL / D3D / Metal).
        var tan_angle_height = isVulcan ? tan_angle_down - tan_angle_up : tan_angle_up - tan_angle_down;
        // Set to near_z for a [-1,1] Z clip space (OpenGL / OpenGL ES).
        // Set to zero for a [0,1] Z clip space (Vulkan / D3D / Metal).
        var offset_z = isOpenGL ? near_z : 0;

        var result = new Matrix4x4();
        // normal projection
        result.m00 = 2.0f / tan_angle_width;
        result.m01 = 0.0f;
        result.m02 = (tan_angle_right + tan_angle_left) / tan_angle_width;
        result.m03 = 0.0f;

        result.m10 = 0.0f;
        result.m11 = 2.0f / tan_angle_height;
        result.m12 = (tan_angle_up + tan_angle_down) / tan_angle_height;
        result.m13 = 0.0f;

        result.m20 = 0.0f;
        result.m21 = 0.0f;
        result.m22 = -(far_z + offset_z) / (far_z - near_z);
        result.m23 = -(far_z * (near_z + offset_z)) / (far_z - near_z);

        result.m30 = 0.0f;
        result.m31 = 0.0f;
        result.m32 = -1.0f;
        result.m33 = 0.0f;
        return result;
    }

    public static Matrix4x4 CreateProjectionFov(FovData fov, float near_z, float far_z)
    {
        var tan_left = Mathf.Tan(fov.angle_left);
        var tan_right = Mathf.Tan(fov.angle_right);
        var tan_down = Mathf.Tan(fov.angle_down);
        var tan_up = Mathf.Tan(fov.angle_up);
        return CreateProjection(tan_left, tan_right, tan_up, tan_down, near_z, far_z);
    }
}