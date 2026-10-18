using System;
using System.IO;
using Unity.Collections;
using UnityEngine;

namespace Wick.Game
{
    /// <summary>Turns I420 frames into a video file.</summary>
    public interface IClipEncoder
    {
        /// <summary>False if this device can't encode.</summary>
        bool Open(string path, int width, int height, int fps, int bitrate);

        void AddFrame(NativeArray<byte> i420);

        /// <returns>Frames written.</returns>
        long Close();
    }

    public static class ClipEncoders
    {
        /// <summary>
        /// Android: hardware H.264 through WickClipEncoder.java. Editor: a stand-in that writes a
        /// few frames back out as PNGs, to check the picture. iOS: not yet (AVAssetWriter wrapper
        /// to come with the iOS build pipeline).
        /// </summary>
        public static IClipEncoder Create()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return new AndroidClipEncoder();
#elif UNITY_EDITOR
            return new PreviewClipEncoder();
#else
            return null;
#endif
        }
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    /// <summary>Hands each frame to Java as a direct ByteBuffer over the readback's memory: no copies in C#.</summary>
    public sealed class AndroidClipEncoder : IClipEncoder
    {
        private AndroidJavaObject _encoder;
        private IntPtr _encodeFrame;

        public bool Open(string path, int width, int height, int fps, int bitrate)
        {
            try
            {
                _encoder = new AndroidJavaObject("com.uprisestudios.wick.WickClipEncoder", path, width, height, fps, bitrate);
                _encodeFrame = AndroidJNIHelper.GetMethodID(_encoder.GetRawClass(), "encodeFrame", "(Ljava/nio/ByteBuffer;)Z");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Clip encoder unavailable: " + e.Message);
                _encoder = null;
                return false;
            }
        }

        public void AddFrame(NativeArray<byte> i420)
        {
            IntPtr buffer = AndroidJNI.NewDirectByteBuffer(i420);
            try
            {
                var args = new jvalue[1];
                args[0].l = buffer;
                for (int attempt = 0; attempt < 20; attempt++)
                {
                    if (AndroidJNI.CallBooleanMethod(_encoder.GetRawObject(), _encodeFrame, args))
                    {
                        return;
                    }
                }

                Debug.LogWarning("Clip encoder dropped a frame");
            }
            finally
            {
                AndroidJNI.DeleteLocalRef(buffer);
            }
        }

        public long Close()
        {
            long frames = _encoder?.Call<long>("finish") ?? 0;
            _encoder?.Dispose();
            _encoder = null;
            return frames;
        }
    }
#endif

    /// <summary>
    /// Editor stand-in: counts frames and decodes every 30th back to RGB as a PNG next to the
    /// output path, so the colour conversion and orientation can be checked without a phone.
    /// </summary>
    public sealed class PreviewClipEncoder : IClipEncoder
    {
        private string _dir;
        private int _width;
        private int _height;
        private long _frames;

        public bool Open(string path, int width, int height, int fps, int bitrate)
        {
            _dir = Path.Combine(Path.GetDirectoryName(path) ?? ".", Path.GetFileNameWithoutExtension(path) + "-frames");
            Directory.CreateDirectory(_dir);
            _width = width;
            _height = height;
            _frames = 0;
            return true;
        }

        public void AddFrame(NativeArray<byte> i420)
        {
            if (_frames % 30 == 0)
            {
                File.WriteAllBytes(Path.Combine(_dir, "frame-" + _frames.ToString("D4") + ".png"), ToRgb(i420, _width, _height).EncodeToPNG());
            }

            _frames++;
        }

        public long Close()
        {
            return _frames;
        }

        /// <summary>I420 back to an RGB texture (BT.601 video range), top row first.</summary>
        public static Texture2D ToRgb(NativeArray<byte> i420, int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            var pixels = new Color32[w * h];
            int ySize = w * h;
            int cSize = ySize / 4;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int c = (y / 2) * (w / 2) + x / 2;
                    float Y = 1.164f * (i420[y * w + x] - 16);
                    float U = i420[ySize + c] - 128;
                    float V = i420[ySize + cSize + c] - 128;
                    byte r = (byte)Mathf.Clamp(Y + 1.596f * V, 0, 255);
                    byte g = (byte)Mathf.Clamp(Y - 0.392f * U - 0.813f * V, 0, 255);
                    byte b = (byte)Mathf.Clamp(Y + 2.017f * U, 0, 255);
                    pixels[(h - 1 - y) * w + x] = new Color32(r, g, b, 255); // Texture2D rows run bottom-up
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }
    }
}
