using System;
using System.IO;

namespace DCP_Ripper.Zipping {
    /// <summary>
    /// A <see cref="Stream"/> that reports read/write progress.
    /// Based on https://stackoverflow.com/questions/42430559
    /// </summary>
    public class StreamWithProgress : Stream {
        readonly Stream stream;
        readonly IProgress<long> readProgress;
        readonly IProgress<long> writeProgress;

        /// <summary>
        /// A <see cref="Stream"/> that reports read/write progress.
        /// </summary>
        /// <param name="stream">Actual stream to use</param>
        /// <param name="readProgress">Read progress reporter</param>
        /// <param name="writeProgress">Write progress reporter</param>
        public StreamWithProgress(Stream stream, IProgress<long> readProgress, IProgress<long> writeProgress) {
            this.stream = stream;
            this.readProgress = readProgress;
            this.writeProgress = writeProgress;
        }

        /// <summary>
        /// <see cref="Stream.CanRead"/>
        /// </summary>
        public override bool CanRead => stream.CanRead;
        /// <summary>
        /// <see cref="Stream.CanSeek"/>
        /// </summary>
        public override bool CanSeek => stream.CanSeek;
        /// <summary>
        /// <see cref="Stream.CanWrite"/>
        /// </summary>
        public override bool CanWrite => stream.CanWrite;
        /// <summary>
        /// <see cref="Stream.Length"/>
        /// </summary>
        public override long Length => stream.Length;
        /// <summary>
        /// <see cref="Stream.Position"/>
        /// </summary>
        public override long Position {
            get => stream.Position;
            set => stream.Position = value;
        }

        /// <summary>
        /// <see cref="Stream.Flush"/>
        /// </summary>
        public override void Flush() => stream.Flush();
        /// <summary>
        /// <see cref="Stream.Seek(long, SeekOrigin)"/>
        /// </summary>
        public override long Seek(long offset, SeekOrigin origin) => stream.Seek(offset, origin);
        /// <summary>
        /// <see cref="Stream.SetLength(long)"/>
        /// </summary>
        public override void SetLength(long value) => stream.SetLength(value);

        /// <summary>
        /// <see cref="Stream.Read(byte[], int, int)"/>
        /// </summary>
        public override int Read(byte[] buffer, int offset, int count) {
            int bytesRead = stream.Read(buffer, offset, count);
            readProgress?.Report(bytesRead);
            return bytesRead;
        }

        /// <summary>
        /// <see cref="Stream.Write(byte[], int, int)"/>
        /// </summary>
        public override void Write(byte[] buffer, int offset, int count) {
            stream.Write(buffer, offset, count);
            writeProgress?.Report(count);
        }
    }
}