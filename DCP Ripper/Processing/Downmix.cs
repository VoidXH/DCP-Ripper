using Cavern.Format;
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
        const long blockSize = 1 << 18;

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
            RIFFWaveWriter writer = new(output, outChannels, input.Length, input.SampleRate, input.Bits);
            writer.WriteHeader();

            long progress = 0;
            float[][] inData = new float[input.ChannelCount][],
                outData = new float[outChannels][];
            for (int i = 0; i < input.ChannelCount; ++i)
                inData[i] = new float[blockSize];
            while (progress < input.Length) {
                input.ReadBlock(inData, 0, blockSize);

                if (auro) {
                    WaveformUtils.Mix(inData[6], inData[0]); // TFL to L
                    WaveformUtils.Mix(inData[7], inData[1]); // TFR to R
                    WaveformUtils.Mix(inData[8], inData[2]); // TFC to C
                    for (int j = 0; j < 6; j += j == 2 ? 2: 1) // GV to mains
                        WaveformUtils.Mix(inData[9], inData[j], gvGain);
                    WaveformUtils.Mix(inData[10], inData[4]); // TSL to SL
                    WaveformUtils.Mix(inData[11], inData[5]); // TSR to SR
                } else {
                    // DCP input order to PC input order
                    (inData[4], inData[10]) = (inData[10], inData[4]); // Swap SL and RL
                    (inData[5], inData[11]) = (inData[11], inData[5]); // Swap SR and RR
                    (inData[6], inData[10]) = (inData[10], inData[6]); // Move SL to HI
                    (inData[7], inData[11]) = (inData[11], inData[7]); // Move SR to VI

                    // SDDS downmix
                    WaveformUtils.Mix(inData[8], inData[0], minus3dB); // LC to L
                    WaveformUtils.Mix(inData[8], inData[2], minus3dB); // LC to C
                    WaveformUtils.Mix(inData[9], inData[1], minus3dB); // RC to C
                    WaveformUtils.Mix(inData[9], inData[2], minus3dB); // RC to R
                }

                Array.Copy(inData, outData, outChannels);
                writer.WriteBlock(outData, 0, Math.Min(blockSize, input.Length - progress));
                progress += blockSize;
            }
            writer.Dispose();
        }

        /// <summary>
        /// Forces a 5.1 output, without any gain change.
        /// This might clip 7.1 content, but solves if 5.1 channels are mixed to rears.
        /// </summary>
        public static void GainKeeping51(RIFFWaveReader input, string output) {
            RIFFWaveWriter writer = new(output, 6, input.Length, input.SampleRate, input.Bits);
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
            writer.Dispose();
        }
    }
}