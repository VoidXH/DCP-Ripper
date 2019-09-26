namespace DCP_Ripper {
    public enum ContentType {
        UNK_Unknown,
        FTR_Feature,
        SHR_Short,
        TLR_Trailer,
        TST_Test,
        XSN_Transitional,
        RTG_RatingTag,
        TSR_Teaser,
        POL_Policy,
        PSA_PublicServiceAnnouncement,
        ADV_Advertisement,
        PRO_Promotion
    }

    /// <summary>
    /// Picture aspect ratio.
    /// </summary>
    public enum Framing {
        /// <summary>
        /// Not defined
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

    public enum Resolution {
        _Unknown,
        _2K,
        _4K
    }

    public enum AudioTrack {
        Unknown,
        Stereo,
        Surround__5_1,
        Surround__7_1,
        SDDS,
        Atmos,
        Auro,
        AuroMax,
        DTS___X,
        Cavern,
        CavernXL,
    }

    public enum Version {
        _OV_OriginalVersion,
        _VF_VersionFile
    }
}
