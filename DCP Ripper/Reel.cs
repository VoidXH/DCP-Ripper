namespace DCP_Ripper {
    /// <summary>
    /// A single reel of content.
    /// </summary>
    public struct Reel {
        /// <summary>
        /// Video track path.
        /// </summary>
        public string videoFile;
        /// <summary>
        /// Audio track path.
        /// </summary>
        public string audioFile;
        /// <summary>
        /// This content is 3D.
        /// </summary>
        public bool is3D;
        /// <summary>
        /// First usable frame of the video track.
        /// </summary>
        public int videoStartFrame;
        /// <summary>
        /// First usable position of the audio track in video frames.
        /// </summary>
        public int audioStartFrame;
        /// <summary>
        /// Duration of both tracks in video frames.
        /// </summary>
        public int duration;
        /// <summary>
        /// Frame rate of the content.
        /// </summary>
        public int framerate;
        /// <summary>
        /// This content is encrypted and can't be processed.
        /// </summary>
        public bool needsKey;
    }
}
