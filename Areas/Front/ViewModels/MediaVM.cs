﻿using System.Collections.Generic;
using Bonsai.Code.Tools;
using Bonsai.Data.Models;

namespace Bonsai.Areas.Front.ViewModels
{
    /// <summary>
    /// Details of a single media file.
    /// </summary>
    public class MediaVM
    {
        /// <summary>
        /// URL of the media file to display inline.
        /// </summary>
        public string MediaPath { get; set; }

        /// <summary>
        /// URL of the full-sized media (for photos).
        /// </summary>
        public string FullPath { get; set; }

        /// <summary>
        /// Type of the media (photo, video, etc.).
        /// </summary>
        public MediaType Type { get; set; }

        /// <summary>
        /// Description of the media (HTML).
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Tagged entities on the photo.
        /// </summary>
        public IReadOnlyList<MediaTagVM> Tags { get; set; }

        /// <summary>
        /// Date of the media file's creation.
        /// </summary>
        public FuzzyDate? Date { get; set; }

        /// <summary>
        /// Related location.
        /// </summary>
        public PageTitleVM Location { get; set; }

        /// <summary>
        /// Related event.
        /// </summary>
        public PageTitleVM Event { get; set; }
    }
}
