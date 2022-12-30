using System;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using BepInEx.Logging;
using UnityEngine;

namespace TCA_VR.Extras;

public static class Deserializer
{
    public static Vector3 Vector3(Stream stream)
    {
        var bytes = new byte[sizeof(float) * 3];
        stream.Read(bytes, 0, bytes.Length);
        return new Vector3
        {
            x = BitConverter.ToSingle(bytes, sizeof(float) * 0),
            y = BitConverter.ToSingle(bytes, sizeof(float) * 1),
            z = -BitConverter.ToSingle(bytes, sizeof(float) * 2),
        };
    }

    public static Quaternion Quaternion(Stream stream)
    {
        var bytes = new byte[sizeof(float) * 4];
        stream.Read(bytes, 0, bytes.Length);
        return new Quaternion
        {
            w = BitConverter.ToSingle(bytes, sizeof(float) * 0),
            x = -BitConverter.ToSingle(bytes, sizeof(float) * 1),
            y = -BitConverter.ToSingle(bytes, sizeof(float) * 2),
            z = BitConverter.ToSingle(bytes, sizeof(float) * 3),
        };
    }
}

public struct FovData
{
    public float angle_up;
    public float angle_down;
    public float angle_right;
    public float angle_left;

    public static FovData Deserialize(Stream stream)
    {
        var bytes = new byte[sizeof(float) * 4];
        stream.Read(bytes, 0, bytes.Length);
        return new FovData
        {
            angle_up = BitConverter.ToSingle(bytes, sizeof(float) * 0),
            angle_down = BitConverter.ToSingle(bytes, sizeof(float) * 1),
            angle_right = BitConverter.ToSingle(bytes, sizeof(float) * 2),
            angle_left = BitConverter.ToSingle(bytes, sizeof(float) * 3),
        };
    }
}

public struct HeadsetPosData
{
    public Vector3 leftEyePos;
    public Vector3 rightEyePos;
    public Quaternion leftEyeQuaternion;
    public Quaternion rightEyeQuaternion;

    public static HeadsetPosData FromEyes(EyeData leftEye, EyeData rightEye) =>
        new HeadsetPosData
        {
            leftEyePos = leftEye.pos,
            rightEyePos = rightEye.pos,
            leftEyeQuaternion = leftEye.quaternion,
            rightEyeQuaternion = rightEye.quaternion,
        };

    public HeadsetPosData sub(HeadsetPosData other) =>
        new HeadsetPosData
        {
            leftEyePos = leftEyePos - other.leftEyePos,
            rightEyePos = rightEyePos - other.rightEyePos,
            leftEyeQuaternion = leftEyeQuaternion * Quaternion.Inverse(other.leftEyeQuaternion),
            rightEyeQuaternion = rightEyeQuaternion * Quaternion.Inverse(other.rightEyeQuaternion),
        };
};

public struct EyeData
{
    public Vector3 pos;
    public Quaternion quaternion;
    public FovData fov;

    public static EyeData Deserialize(Stream stream) =>
        new EyeData
        {
            pos = Deserializer.Vector3(stream),
            quaternion = Deserializer.Quaternion(stream),
            fov = FovData.Deserialize(stream),
        };
};

public struct Data
{
    public EyeData leftEye;
    public EyeData rightEye;

    public static Data Deserialize(Stream stream) =>
        new Data
        {
            leftEye = EyeData.Deserialize(stream),
            rightEye = EyeData.Deserialize(stream),
        };
}

public class HeadsetListener
{
    Data synced_data;
    bool hasBeenRead = true;
    ManualLogSource Logger;

    public HeadsetListener(ManualLogSource logger)
    {
        Logger = logger;
        var readThread = new System.Threading.Thread(ReadThread);
        readThread.Start();
    }

    private void ReadThread()
    {
        Logger.LogWarning("I'm Running Stuff");
        var f = new BinaryFormatter();
        var server = new NamedPipeServerStream("tca_vr_headset_data", PipeDirection.In);
        {
            server.WaitForConnection();
            while (true)
            {
                var data = Data.Deserialize(server);
                // Logger.LogInfo("Got data: " + data.leftEyePos.ToUnityVec3());
                lock (this)
                {
                    hasBeenRead = false;
                    synced_data = data;
                }
            }
        }
    }

    public bool HasBeenRead()
    {
        lock (this)
            return hasBeenRead;
    }
    public Data Read()
    {
        lock (this)
        {
            hasBeenRead = true;
            return synced_data;
        }
    }
};