using Cavern;
using Cavern.Format;
using Cavern.Remapping;
using Cavern.Utilities;
using System;

namespace DCP_Ripper.Processing {
    /// <summary>
    /// Downmixing method implementations.
    /// </summary>
    public static class Downmix {
        /// <summary>
        /// 1 MB per channel at 32 bits.
        /// </summary>
        const int blockSize = 1 << 18;

        /// <summary>
        /// Gain for -3 dB.
        /// </summary>
        const float minus3dB = .707f;

        /// <summary>
        /// Constant power gain for downmixing God's Voice to 5.0.
        /// </summary>
        const float gvGain = .4472135955f;

        /// <summary>
        /// 7.1 if available, stripping HI/VI/sync. 5.1 should be just returned before using this.
        /// </summary>
        public static void Surround(RIFFWaveReader input, bool auro, string output) {
            int outChannels = auro ? 6 : 8;
            using RIFFWaveWriter writer = new(output, outChannels, input.Length, input.SampleRate, input.Bits);
            writer.WriteHeader();

            long progress = 0;
            float[][] inData = new float[input.ChannelCount][],
                outData = new float[outChannels][];
            for (int i = 0; i < input.ChannelCount; ++i)
                inData[i] = new float[blockSize];
            while (progress < input.Length) {
                input.ReadBlock(inData, 0, blockSize);

                if (auro) {
                    for (int j = 6; j < Math.Min(inData.Length, 12); j += j == 8 ? 2 : 1)
                        WaveformUtils.Mix(inData[j], inData[j - 6]); // Top channels to their ground counterparts
                    if (inData.Length > 9)
                        for (int j = 0; j < 6; j += j == 2 ? 2: 1)
                            WaveformUtils.Mix(inData[9], inData[j], gvGain); // GV to mains
                } else {
                    if (inData.Length >= 12) { // DCP input order to PC input order
                        (inData[4], inData[10]) = (inData[10], inData[4]); // Swap SL and RL
                        (inData[5], inData[11]) = (inData[11], inData[5]); // Swap SR and RR
                        (inData[6], inData[10]) = (inData[10], inData[6]); // Move SL to HI
                        (inData[7], inData[11]) = (inData[11], inData[7]); // Move SR to VI
                    }

                    if (inData.Length >= 10) { // SDDS downmix
                        WaveformUtils.Mix(inData[8], inData[0], minus3dB); // LC to L
                        WaveformUtils.Mix(inData[8], inData[2], minus3dB); // LC to C
                        WaveformUtils.Mix(inData[9], inData[1], minus3dB); // RC to C
                        WaveformUtils.Mix(inData[9], inData[2], minus3dB); // RC to R
                    }
                }

                Array.Copy(inData, outData, outChannels);
                writer.WriteBlock(outData, 0, Math.Min(blockSize, input.Length - progress));
                progress += blockSize;
            }
        }

        /// <summary>
        /// Forces a 5.1 output, without any gain change.
        /// This might clip 7.1 content, but solves if 5.1 channels are mixed to rears.
        /// </summary>
        public static void GainKeeping51(RIFFWaveReader input, string output) {
            using RIFFWaveWriter writer = new(output, 6, input.Length, input.SampleRate, input.Bits);
            writer.WriteHeader();

            long progress = 0;
            float[][] inData = new float[input.ChannelCount][],
                outData = new float[6][];
            for (int i = 0; i < input.ChannelCount; ++i)
                inData[i] = new float[blockSize];
            Array.Copy(inData, outData, outData.Length);
            while (progress < input.Length) {
                input.ReadBlock(inData, 0, blockSize);
                // 6-7 are hearing/visually impaired tracks, 12+ are sync signals
                for (int i = 8; i < Math.Min(inData.Length, 12); ++i)
                    WaveformUtils.Mix(inData[i], inData[4 + i % 2]);
                writer.WriteBlock(outData, 0, Math.Min(blockSize, input.Length - progress));
                progress += blockSize;
            }
        }

        /// <summary>
        /// Auto-detects the DCP's channel layout and mixes it to the user defined target layout set in the Cavern Driver.
        /// </summary>
        public static void Cavern(RIFFWaveReader input, string output) {
            using Remapper mapper = new(input.ChannelCount, blockSize, true);
            int channels = Listener.Channels.Length;
            using RIFFWaveWriter writer = new(output, channels, input.Length, input.SampleRate, input.Bits);
            writer.WriteHeader();

            long progress = 0;
            float[] buffer = new float[input.ChannelCount * blockSize];
            while (progress < input.Length) {
                input.ReadBlock(buffer, 0, buffer.Length);
                float[] result = mapper.Update(buffer, input.ChannelCount);
                writer.WriteBlock(result, 0, Math.Min(result.Length, channels * (input.Length - progress)));
                progress += blockSize;
            }
        }
    }
}