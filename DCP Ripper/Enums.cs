namespace DCP_Ripper {
    /// <summary>
    /// Content category.
    /// </summary>
    public enum ContentType {
        /// <summary>
        /// No data for content type in the title.
        /// </summary>
        UNK_Unknown,
        /// <summary>
        /// Full movie.
        /// </summary>
        FTR_Feature,
        /// <summary>
        /// Short movie.
        /// </summary>
        SHR_Short,
        /// <summary>
        /// Movie trailer. TV trailers should be <see cref="ADV_Advertisement"/>.
        /// </summary>
        TLR_Trailer,
        /// <summary>
        /// Test clip.
        /// </summary>
        TST_Test,
        /// <summary>
        /// Transitional clip ("wish"), includes Digital Black for spacing.
        /// </summary>
        XSN_Transitional,
        /// <summary>
        /// Pre-content rating tag.
        /// </summary>
        RTG_RatingTag,
        /// <summary>
        /// Short movie trailer.
        /// </summary>
        TSR_Teaser,
        /// <summary>
        /// Local theatre rules (like "turn off your phones").
        /// </summary>
        POL_Policy,
        /// <summary>
        /// Government or charity advertisements.
        /// </summary>
        PSA_PublicServiceAnnouncement,
        /// <summary>
        /// Promotion content other than <see cref="TLR_Trailer"/>, <see cref="TSR_Teaser"/>, or <see cref="PRO_Promotion"/>.
        /// </summary>
        ADV_Advertisement,
        /// <summary>
        /// Non-trailer promotions for movies that are usually shown at conventions.
        /// </summary>
        PRO_Promotion
    }

    /// <summary>
    /// Picture aspect ratio.
    /// </summary>
    public enum Framing {
        /// <summary>
        /// No data for framing in the title.
        /// </summary>
        _Unknown,
        /// <summary>
        /// 1.19 [119]
        /// </summary>
        _119,
        /// <summary>
        /// 4:3 = 1.33 [133]
        /// </summary>
        _133,
        /// <summary>
        /// 1.375 [137]
        /// </summary>
        _137_Academy,
        /// <summary>
        /// 1.66 [166]
        /// </summary>
        _166,
        /// <summary>
        /// 16:9 = 1.78 [178]
        /// </summary>
        _178,
        /// <summary>
        /// 1.85 [F]
        /// </summary>
        _185_Flat,
        /// <summary>
        /// 2.35 [S]
        /// </summary>
        _235_Scope,
        /// <summary>
        /// 2.39 [S]
        /// </summary>
        _239_Scope,
        /// <summary>
        /// 1.85 [F]
        /// </summary>
        _F_Flat,
        /// <summary>
        /// 2.35 or 2.39 [S]
        /// </summary>
        _S_Scope,
        /// <summary>
        /// 1.9 [C]
        /// </summary>
        _C_FullContainer
    }

    /// <summary>
    /// Frame width.
    /// </summary>
    public enum Resolution {
        /// <summary>
        /// No data for frame width in the title.
        /// </summary>
        _Unknown,
        /// <summary>
        /// 2048 pixels.
        /// </summary>
        _2K,
        /// <summary>
        /// 4096 pixels.
        /// </summary>
        _4K
    }

    /// <summary>
    /// Master audio track type.
    /// </summary>
    public enum AudioTrack {
        /// <summary>
        /// No data for audio track in the title.
        /// </summary>
        Unknown,
        /// <summary>
        /// Left and right channels only.
        /// </summary>
        Stereo,
        /// <summary>
        /// 5.1 (side) surround sound.
        /// </summary>
        Surround__5_1,
        /// <summary>
        /// 7.1 (side/rear) surround sound.
        /// </summary>
        Surround__7_1,
        /// <summary>
        /// Sony Dynamic Digital Sound 7.1 (front/side). Use Cavern for remixing.
        /// </summary>
        SDDS,
        /// <summary>
        /// Dolby Atmos companion track next to an original 5.1 or 7.1 track. Use Cavern for sync stream removal.
        /// </summary>
        Atmos,
        /// <summary>
        /// Barco Auro track embedded for active decoding in a 5.1 or 7.1 track.
        /// </summary>
        Auro,
        /// <summary>
        /// Barco AuroMax companion track next to an original Auro 11.1 or 13.1 track. Use Cavern for downmix.
        /// </summary>
        AuroMax,
        /// <summary>
        /// DTS:X audio track.
        /// </summary>
        DTS___X,
        /// <summary>
        /// Raw Cavern 9.1 surround sound.
        /// </summary>
        Cavern,
        /// <summary>
        /// Raw Cavern 10.1 surround sound including bottom surround.
        /// </summary>
        CavernXL,
        /// <summary>
        /// IMAX 5.0.
        /// </summary>
        IMAX5,
        /// <summary>
        /// IMAX 6.0 (5.0 + center height).
        /// </summary>
        IMAX6,
        /// <summary>
        /// IMAX 12-track.
        /// </summary>
        IMAX12,
    }

    /// <summary>
    /// Content format.
    /// </summary>
    public enum Version {
        /// <summary>
        /// Base DCP.
        /// </summary>
        _OV_OriginalVersion,
        /// <summary>
        /// Override DCP.
        /// </summary>
        _VF_VersionFile
    }

    /// <summary>
    /// 3D content ripping modes.
    /// </summary>
    public enum Mode3D {
        /// <summary>
        /// Horizontally stacked half resolution left/right eyes.
        /// </summary>
        HalfSideBySide,
        /// <summary>
        /// Vertically stacked half resolution left/right eyes.
        /// </summary>
        HalfOverUnder,
        /// <summary>
        /// Horizontally stacked left/right eyes.
        /// </summary>
        SideBySide,
        /// <summary>
        /// Vertically stacked left/right eyes.
        /// </summary>
        OverUnder,
        /// <summary>
        /// Alternating left/right eyes in a double framerate footage.
        /// </summary>
        Interop,
        /// <summary>
        /// 2D output with left eye retain.
        /// </summary>
        LeftEye,
        /// <summary>
        /// 2D output with right eye retain.
        /// </summary>
        RightEye
    }

    /// <summary>
    /// Downmixing methods.
    /// </summary>
    public enum Downmixer {
        /// <summary>
        /// Passes the audio track straight to FFmpeg. This might not work.
        /// </summary>
        Bypass,
        /// <summary>
        /// Forces a 5.1 output, without any gain change.
        /// This might clip 7.1 content, but solves if 5.1 channels are mixed to rears.
        /// </summary>
        GainKeeping51,
        /// <summary>
        /// 5.1 or 7.1 if available, stripping HI/VI/sync.
        /// </summary>
        Surround,
        /// <summary>
        /// Applies the -mapping_family 255 argument.
        /// </summary>
        RawMapping
    }
}