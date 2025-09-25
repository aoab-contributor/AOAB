﻿using AOABO.Config;
using AOABO.Omnibus;

namespace AOABO.Chapters
{
    public class Gallery : MoveableChapter
    {
        public List<string> SplashImages { get; set; } = new List<string>();
        public List<string> ChapterImages { get; set; } = new List<string>();
        public string AltName { get; set; } = string.Empty;

        private bool StartOfBook = false;

        public Gallery? GetChapter(bool startOfBook, bool splashImages, bool chapterImages)
        {
            if (!splashImages && !chapterImages)
            {
                return null;
            }

            var c = new Gallery
            {
                SortOrder = startOfBook ? EarlySortOrder : LateSortOrder,
                Year = startOfBook ? EarlyYear : LateYear,
                Season = startOfBook ? EarlySeason : LateSeason,
                StartOfBook = startOfBook,
                Volume = Volume,
                ChapterName = (Configuration.Options_.OutputStructure == OutputStructure.Volumes ? AltName ?? ChapterName : ChapterName),
                EarlySeason = EarlySeason,
                EarlyYear = EarlyYear,
                LateSeason = LateSeason,
                LateYear = LateYear,
                ProcessedInFanbooks = ProcessedInFanbooks,
                ProcessedInPartFive = ProcessedInPartFive,
                ProcessedInPartFour = ProcessedInPartFour,
                ProcessedInPartOne = ProcessedInPartOne,
                ProcessedInPartThree = ProcessedInPartThree,
                ProcessedInPartTwo = ProcessedInPartTwo,
                OriginalFilenames = new List<string>()
            };

            if (splashImages)
            {
                c.OriginalFilenames.AddRange(SplashImages);
            }

            if (chapterImages)
            {
                c.OriginalFilenames.AddRange(ChapterImages);
            }
            return c;
        }

        protected override string GetPartSubFolder()
        {
            if (StartOfBook)
                return $"{base.GetPartSubFolder()}\\01-Inserts";
            else
                return $"{base.GetPartSubFolder()}\\{Volume}xx-{getVolumeName()} Bonus";
        }

        protected override string GetVolumeSubFolder()
        {
            if (StartOfBook)
                return $"{base.GetVolumeSubFolder()}\\01-Inserts";
            else
                return $"{base.GetVolumeSubFolder()}\\{Volume}xx-{getVolumeName()} Bonus";
        }

        protected override string GetFlatSubFolder()
        {
            if (StartOfBook)
                return "01-Inserts";
            else
                return "04-Gallery";
        }

        protected override string GetYearsSubFolder()
        {
            if (StartOfBook)
                return base.GetYearsSubFolder();
            else
                return $"{base.GetYearsSubFolder()}\\{Volume}xx-{getVolumeName()} Bonus";
        }

        protected override bool IsEarly()
        {
            return StartOfBook;
        }

        public override CollectionChapter GetCollectionChapter()
        {
            throw new NotImplementedException();
        }
    }
}