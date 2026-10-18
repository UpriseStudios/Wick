package com.uprisestudios.wick;

import android.media.Image;
import android.media.MediaCodec;
import android.media.MediaCodecInfo;
import android.media.MediaFormat;
import android.media.MediaMuxer;

import java.io.IOException;
import java.nio.ByteBuffer;

/**
 * Encodes Wick's share clips: I420 frames from Unity in, an H.264 MP4 out, using the device's
 * hardware encoder (MediaCodec) and MediaMuxer. Frames are written through the codec's input
 * Image, which describes each plane's row and pixel stride, so it works whatever YUV layout the
 * device's encoder wants.
 */
public final class WickClipEncoder {
    private static final String MIME = MediaFormat.MIMETYPE_VIDEO_AVC;
    private static final long TIMEOUT_US = 10_000;

    private final MediaCodec codec;
    private final MediaMuxer muxer;
    private final MediaCodec.BufferInfo info = new MediaCodec.BufferInfo();
    private final int width;
    private final int height;
    private final int fps;
    private int track = -1;
    private boolean muxing;
    private long frames;

    public WickClipEncoder(String path, int width, int height, int fps, int bitrate) throws IOException {
        this.width = width;
        this.height = height;
        this.fps = fps;
        MediaFormat format = MediaFormat.createVideoFormat(MIME, width, height);
        format.setInteger(MediaFormat.KEY_COLOR_FORMAT, MediaCodecInfo.CodecCapabilities.COLOR_FormatYUV420Flexible);
        format.setInteger(MediaFormat.KEY_BIT_RATE, bitrate);
        format.setInteger(MediaFormat.KEY_FRAME_RATE, fps);
        format.setInteger(MediaFormat.KEY_I_FRAME_INTERVAL, 1);
        codec = MediaCodec.createEncoderByType(MIME);
        codec.configure(format, null, null, MediaCodec.CONFIGURE_FLAG_ENCODE);
        codec.start();
        muxer = new MediaMuxer(path, MediaMuxer.OutputFormat.MUXER_OUTPUT_MPEG_4);
    }

    /** One frame: I420 bytes (Y plane, then U, then V), top row first. Returns false if the codec was busy. */
    public boolean encodeFrame(ByteBuffer i420) {
        int index = codec.dequeueInputBuffer(TIMEOUT_US * 5);
        if (index < 0) {
            drain(false);
            return false;
        }

        Image image = codec.getInputImage(index);
        Image.Plane[] planes = image.getPlanes();
        int ySize = width * height;
        int cSize = ySize / 4;
        copyPlane(i420, 0, width, height, planes[0]);
        copyPlane(i420, ySize, width / 2, height / 2, planes[1]);
        copyPlane(i420, ySize + cSize, width / 2, height / 2, planes[2]);
        long ptsUs = frames * 1_000_000L / fps;
        codec.queueInputBuffer(index, 0, ySize + 2 * cSize, ptsUs, 0);
        frames++;
        drain(false);
        return true;
    }

    private static void copyPlane(ByteBuffer src, int offset, int w, int h, Image.Plane plane) {
        ByteBuffer dst = plane.getBuffer();
        int rowStride = plane.getRowStride();
        int pixelStride = plane.getPixelStride();
        if (pixelStride == 1 && rowStride == w) {
            ByteBuffer slice = src.duplicate();
            slice.position(offset);
            slice.limit(offset + w * h);
            dst.put(slice);
            return;
        }

        for (int y = 0; y < h; y++) {
            int row = y * rowStride;
            int from = offset + y * w;
            if (pixelStride == 1) {
                ByteBuffer slice = src.duplicate();
                slice.position(from);
                slice.limit(from + w);
                dst.position(row);
                dst.put(slice);
            } else {
                for (int x = 0; x < w; x++) {
                    dst.put(row + x * pixelStride, src.get(from + x));
                }
            }
        }
    }

    /** Ends the stream and closes the file. Returns the number of frames written. */
    public long finish() {
        int index = -1;
        for (int i = 0; i < 100 && index < 0; i++) {
            index = codec.dequeueInputBuffer(TIMEOUT_US);
            if (index < 0) {
                drain(false);
            }
        }

        if (index >= 0) {
            codec.queueInputBuffer(index, 0, 0, frames * 1_000_000L / fps, MediaCodec.BUFFER_FLAG_END_OF_STREAM);
        }

        drain(true);
        codec.stop();
        codec.release();
        if (muxing) {
            muxer.stop();
        }

        muxer.release();
        return frames;
    }

    private void drain(boolean untilEnd) {
        int idle = 0;
        while (true) {
            int index = codec.dequeueOutputBuffer(info, untilEnd ? TIMEOUT_US * 10 : 0);
            if (index == MediaCodec.INFO_TRY_AGAIN_LATER) {
                if (!untilEnd || ++idle > 50) {
                    return; // up to half a second for the end of stream
                }

                continue;
            }

            if (index == MediaCodec.INFO_OUTPUT_FORMAT_CHANGED) {
                track = muxer.addTrack(codec.getOutputFormat());
                muxer.start();
                muxing = true;
                continue;
            }

            if (index < 0) {
                continue;
            }

            ByteBuffer data = codec.getOutputBuffer(index);
            boolean config = (info.flags & MediaCodec.BUFFER_FLAG_CODEC_CONFIG) != 0;
            if (!config && info.size > 0 && muxing && data != null) {
                data.position(info.offset);
                data.limit(info.offset + info.size);
                muxer.writeSampleData(track, data, info);
            }

            codec.releaseOutputBuffer(index, false);
            if ((info.flags & MediaCodec.BUFFER_FLAG_END_OF_STREAM) != 0) {
                return;
            }
        }
    }
}
