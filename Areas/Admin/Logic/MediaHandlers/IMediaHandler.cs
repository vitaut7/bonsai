﻿using System.Drawing;
using Bonsai.Data.Models;

namespace Bonsai.Areas.Admin.Logic.MediaHandlers
{
    /// <summary>
    /// Common interface for handling an uploaded media file.
    /// </summary>
    public interface IMediaHandler
    {
        /// <summary>
        /// Flag indicating that the media is immediately available (does not need to wait for encoding).
        /// </summary>
        bool IsImmediate { get; }

        /// <summary>
        /// List of mime types this handler accepts.
        /// </summary>
        string[] SupportedMimeTypes { get; }

        /// <summary>
        /// Media file classification.
        /// </summary>
        MediaType MediaType { get; }

        /// <summary>
        /// Creates thumbnail files for this media file.
        /// </summary>
        Image ExtractThumbnail(string path, string mime);
    }
}
