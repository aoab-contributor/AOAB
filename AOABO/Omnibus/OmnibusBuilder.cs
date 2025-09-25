﻿using AOABO.Chapters;
using AOABO.Config;
using Core.Processor;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace AOABO.Omnibus
{
    public class OmnibusBuilder
    {
        //TODO: Cleanup part selection to separate selector from description - add checks to ensure valid selection
        public const string ScopeEntireSeries = "0: Entire Series";
        public const string ScopePart1 = "1: Part One (Daughter of a Soldier)";
        public const string ScopePart2 = "2: Part Two (Apprentice Shrine Maiden)";
        public const string ScopePart3 = "3: Part Three (Adopted Daughter of an Archduke)";
        public const string ScopePart4 = "4: Part Four (Founder of the Royal Academy's So-Called Library Committee)";
        public const string ScopePart5 = "5: Part Five (Avatar of a Goddess)";
		public const string ScopePart6 = "6: Hannelore's Fifth Year at the Royal Academy";
        public const string ScopeFanbooks = "7: Fanbooks";
        public enum PartToProcess
        {
            EntireSeries,
            PartOne,
            PartTwo,
            PartThree,
            PartFour,
            PartFive,
            Fanbooks,
            Hannelore
        }

        private static Regex chapterTitleRegex = new Regex("<h1>[\\s\\S]*?<\\/h1>");

        public static async Task InteractivelyBuildOmnibus()
        {
            Console.Clear();
            Console.WriteLine("Creating an Ascendance of a Bookworm Omnibus");
            Console.WriteLine();
            Console.WriteLine("How much of the series should be in the output file?");
            Console.WriteLine(ScopeEntireSeries);
            Console.WriteLine(ScopePart1);
            Console.WriteLine(ScopePart2);
            Console.WriteLine(ScopePart3);
            Console.WriteLine(ScopePart4);
            Console.WriteLine(ScopePart5);
            Console.WriteLine(ScopePart6);
            Console.WriteLine(ScopeFanbooks);
            var key = Console.ReadKey();
            Console.WriteLine();

            await BuildOmnibus(key.KeyChar);
        }

        public static async Task BuildOmnibus(char omnibusPart)
        {
            var inputFolder = string.IsNullOrWhiteSpace(Configuration.Options.Folder.InputFolder) ? Directory.GetCurrentDirectory() :
                Configuration.Options.Folder.InputFolder.Length > 1 && Configuration.Options.Folder.InputFolder[1].Equals(':') ? Configuration.Options.Folder.InputFolder : Directory.GetCurrentDirectory() + "\\" + Configuration.Options.Folder.InputFolder;

            var outputFolder = string.IsNullOrWhiteSpace(Configuration.Options.Folder.OutputFolder) ? Directory.GetCurrentDirectory() :
                Configuration.Options.Folder.OutputFolder.Length > 1 && Configuration.Options.Folder.OutputFolder[1].Equals(':') ? Configuration.Options.Folder.OutputFolder : Directory.GetCurrentDirectory() + "\\" + Configuration.Options.Folder.OutputFolder;

            var OverrideDirectory = inputFolder + "\\Overrides\\";

            PartToProcess partScope;
            string bookTitle;
            switch (omnibusPart)
            {
                case '1':
                    partScope = PartToProcess.PartOne;
                    bookTitle = "Ascendance of a Bookworm Part 1 - Daughter of a Soldier";
                    break;
                case '2':
                    partScope = PartToProcess.PartTwo;
                    bookTitle = "Ascendance of a Bookworm Part 2 - Apprentice Shrine Maiden";
                    break;
                case '3':
                    partScope = PartToProcess.PartThree;
                    bookTitle = "Ascendance of a Bookworm Part 3 - Adopted Daughter of an Archduke";
                    break;
                case '4':
                    partScope = PartToProcess.PartFour;
                    bookTitle = "Ascendance of a Bookworm Part 4 - Founder of the Royal Academy's So-Called Library Committee";
                    break;
                case '5':
                    partScope = PartToProcess.PartFive;
                    bookTitle = "Ascendance of a Bookworm Part 5 - Avatar of a Goddess";
                    break;
                case '6':
                    partScope = PartToProcess.Hannelore;
                    bookTitle = "Ascendance of a Bookworm - Hannelore's Fifth Year at the Royal Academy";
                    break;
                case '7':
                    partScope = PartToProcess.Fanbooks;
                    bookTitle = "Ascendance of a Bookworm Fanbooks";
                    break;
                default:
                    partScope = PartToProcess.EntireSeries;
                    bookTitle = "Ascendance of a Bookworm Anthology";
                    break;
            }

            if (Directory.Exists($"{inputFolder}\\inputtemp")) Directory.Delete($"{inputFolder}\\inputtemp", true);
            Directory.CreateDirectory($"{inputFolder}\\inputtemp");

            var epubs = Directory.GetFiles(inputFolder, "*.epub");

            if (!epubs.Any())
                return;

            foreach (var vol in Configuration.VolumeNames)
            {
                try
                {
                    var file = vol.NameMatch(epubs);
                    if (file == null) continue;
                    var volume = Configuration.Volumes.FirstOrDefault(x => x.InternalName.Equals(vol.InternalName));
                    if (volume == null) continue;

                    if ((partScope == PartToProcess.PartOne && !volume.ProcessedInPartOne)
                        || (partScope == PartToProcess.PartTwo && !volume.ProcessedInPartTwo)
                        || (partScope == PartToProcess.PartThree && !volume.ProcessedInPartThree)
                        || (partScope == PartToProcess.PartFour && !volume.ProcessedInPartFour)
                        || (partScope == PartToProcess.PartFive && !volume.ProcessedInPartFive)
                        || (partScope == PartToProcess.Fanbooks && !volume.ProcessedInFanbooks)
                        || (partScope == PartToProcess.Hannelore && !volume.ProcessedInHannelore)) continue;

                    ZipFile.ExtractToDirectory(file, $"{inputFolder}\\inputtemp\\{volume.InternalName}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{ex.Message} while unzipping file {vol.FileName}.epub");
                }
            }

            var outProcessor = new Processor();
            var inProcessor = new Processor();

            await inProcessor.UnpackFolder($"{inputFolder}\\inputtemp");
            await outProcessor.UnpackFolder($"{inputFolder}\\inputtemp");
            outProcessor.Chapters.Clear();

            IFolder folder = Configuration.Options.OutputYearFormat == 0 ? new YearNumberFolder() : new YearFolder();
            Configuration.ReloadVolumes();

            var povChapters = new List<Chapters.MoveableChapter>();
            var missingFiles = new List<string>();

            foreach (var vol in Configuration.VolumeNames)
            {
                try
                {
                    var file = vol.NameMatch(epubs);
                    if (file == null)
                    {
                        missingFiles.Add(vol.FileName);
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"No file found that matches volume {vol.FileName}");
                    Console.WriteLine(ex.Message);
                    continue;
                }
                Volume? volume = null;
                try
                {
                    volume = Configuration.Volumes.FirstOrDefault(x => x.InternalName.Equals(vol.InternalName));
                    if (volume == null) continue;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"No entry in Volumes.json found that matches internal name {vol.InternalName}");
                    Console.WriteLine(ex.Message);
                    continue;
                }

                if (partScope == PartToProcess.PartOne && !volume.ProcessedInPartOne
                    || partScope == PartToProcess.PartTwo && !volume.ProcessedInPartTwo
                    || partScope == PartToProcess.PartThree && !volume.ProcessedInPartThree
                    || partScope == PartToProcess.PartFour && !volume.ProcessedInPartFour
                    || partScope == PartToProcess.PartFive && !volume.ProcessedInPartFive
                    || partScope == PartToProcess.Hannelore && !volume.ProcessedInHannelore) continue;

                Console.WriteLine($"Processing book {volume.InternalName}");

                List<Chapters.Chapter> chapters;
                switch (partScope)
                {
                    case PartToProcess.PartOne:
                        chapters = BuildChapterList(volume, c => c.ProcessedInPartOne);
                        break;
                    case PartToProcess.PartTwo:
                        chapters = BuildChapterList(volume, c => c.ProcessedInPartTwo);
                        break;
                    case PartToProcess.PartThree:
                        chapters = BuildChapterList(volume, c => c.ProcessedInPartThree);
                        break;
                    case PartToProcess.PartFour:
                        chapters = BuildChapterList(volume, c => c.ProcessedInPartFour);
                        break;
                    case PartToProcess.PartFive:
                        chapters = BuildChapterList(volume, c => c.ProcessedInPartFive);
                        break;
                    case PartToProcess.Fanbooks:
                        chapters = BuildChapterList(volume, c => c.ProcessedInFanbooks);
                        break;
                    case PartToProcess.Hannelore:
                        chapters = BuildChapterList(volume, c => c.ProcessedInHannelore);
                        break;
                    default:
                        chapters = BuildChapterList(volume, c => true);
                        break;
                }

                var inChapters = inProcessor.Chapters.Where(x => x.SubFolder.Contains(volume.InternalName)).ToList();
                foreach (var chapter in chapters)
                {

                    try
                    {
                        bool notFirst = false;
                        var newChapter = new Core.Processor.Chapter
                        {
                            Contents = string.Empty,
                            CssFiles = new List<string>(),
                            Name = chapter.ChapterName + ".xhtml",
                            SubFolder = folder.MakeFolder(chapter.GetSubFolder(Configuration.Options.OutputStructure), Configuration.Options.StartYear, chapter.Year),
                            Set = chapter.Set,
                            Priority = chapter.Priority
                        };
                        newChapter.SortOrder = chapter.SortOrder;
                        outProcessor.Chapters.Add(newChapter);


                        if (File.Exists($"{OverrideDirectory}{(chapter as MoveableChapter)?.OverrideName}.xhtml"))
                        {
                            newChapter.Contents = File.ReadAllText(OverrideDirectory + (chapter as MoveableChapter)?.OverrideName + ".xhtml");
                        }
                        else
                        {
                            foreach (var chapterFile in chapter.OriginalFilenames)
                            {
                                try
                                {
                                    var entry = inChapters.First(x => x.Name.Equals(chapterFile));
                                    newChapter.CssFiles.AddRange(entry.CssFiles);
                                    var fileContent = entry.Contents;

                                    if (notFirst)
                                    {
                                        fileContent = fileContent.Replace("<body class=\"nomargin center\">", string.Empty).Replace("<body>", string.Empty);
                                    }
                                    else
                                    {
                                        notFirst = true;
                                    }
                                    newChapter.Contents = string.Concat(newChapter.Contents, fileContent.Replace("</body>", string.Empty));

                                    entry.Processed = true;
                                }
                                catch (Exception ex)
                                {
                                    throw new Exception($"{ex.Message} while processing file {chapterFile}", ex);
                                }
                            }
                        }

                        if (Configuration.Options.Chapter.UpdateChapterNames)
                        {
                            var match = chapterTitleRegex.Match(newChapter.Contents);
                            if(match.Success)
                                newChapter.Contents = newChapter.Contents.Replace(match.Value, $"<h1>{newChapter.Name}</h1>");
                        }
                        if (!string.IsNullOrWhiteSpace(chapter.StartLine))
                        {
                            var location = newChapter.Contents.IndexOf(chapter.StartLine);
                            newChapter.Contents = newChapter.Contents.Substring(location).Replace(chapter.StartLine, $"<body><section><div><h1>{newChapter.Name}</h1>");
                        }

                        if (!string.IsNullOrWhiteSpace(chapter.EndLine))
                        {
                            var location = newChapter.Contents.IndexOf(chapter.EndLine);
                            newChapter.Contents = newChapter.Contents.Substring(0, location);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing chapter {chapter.ChapterName} in book {vol.InternalName}");
                        Console.WriteLine(ex.ToString());
                    }
                }

                if (vol.OutputUnusedFiles)
                {
                    foreach (var entry in inChapters.Where(x => !x.Processed))
                    {
                        Console.WriteLine($"Unprocessed chapter {entry.Name}");
                    }
                }
            }

            outProcessor.Metadata.Add("<meta name=\"cover\" content=\"images/cover.jpg\" />");
            outProcessor.Images.Add(new Core.Processor.Image { Name = "cover.jpg", Referenced = true, OldLocation = "cover.jpg" });

            var coverContents = File.ReadAllText("Reference\\cover.txt");

            outProcessor.Chapters.Add(new Core.Processor.Chapter { Contents = coverContents, Name = "Cover.xhtml", SortOrder = "00", SubFolder = "00-Cover" });

            if (File.Exists($"{bookTitle}.epub")) File.Delete($"{bookTitle}.epub");

            outProcessor.Metadata.Add(@$"<dc:title>{bookTitle}</dc:title>");
            outProcessor.Metadata.Add("<dc:creator id=\"creator01\">Miya Kazuki</dc:creator>");
            outProcessor.Metadata.Add("<meta property=\"display-seq\" refines=\"#creator01\">1</meta>");
            outProcessor.Metadata.Add("<meta property=\"file-as\" refines=\"#creator01\">KAZUKI, MIYA</meta>");
            outProcessor.Metadata.Add("<meta property=\"role\" refines=\"#creator01\" scheme=\"marc:relators\">aut</meta>");
            outProcessor.Metadata.Add("<dc:language>en</dc:language>");
            outProcessor.Metadata.Add("<dc:publisher>J-Novel Club</dc:publisher>");
            outProcessor.Metadata.Add("<dc:identifier id=\"pub-id\">1</dc:identifier>");
            outProcessor.Metadata.Add($"<meta property=\"dcterms:modified\">{DateTime.UtcNow.ToString("yyyy-MM-ddThh:mm:ssZ")}</meta>");

            await outProcessor.FullOutput(outputFolder, false, Configuration.Options.UseHumanReadableFileStructure, Configuration.Options.Folder.DeleteTempFolder, bookTitle, Configuration.Options.Image.MaxWidth, Configuration.Options.Image.MaxHeight, Configuration.Options.Image.Quality);

            if (Directory.Exists($"{inputFolder}\\inputtemp")) Directory.Delete($"{inputFolder}\\inputtemp", true);

            Console.WriteLine();
            if (missingFiles.Any())
            {
                Console.WriteLine("Books that could not be found while making this omnibus:");
                foreach (var file in missingFiles) Console.WriteLine(file);
            }
            Console.WriteLine();

            Console.WriteLine($"\"{bookTitle}\" creation complete. Press any key to continue.");
            Console.ReadKey();
        }

        private static List<Chapters.Chapter> BuildChapterList(Volume volume, Func<Chapters.Chapter, bool> filter)
        {
            var chapters = new List<Chapters.Chapter>();

            if (Configuration.Options.Chapter.UpdateChapterNames)
            {
                volume.POVChapters.ForEach(x => x.ApplyPOVToTitle());
                volume.BonusChapters.ForEach(x => x.ApplyPOVToTitle());
                volume.MangaChapters.ForEach(x => x.ApplyPOVToTitle());
            }

            if (Configuration.Options.Chapter.IncludeRegularChapters)
            {
                if (!Configuration.Options.Image.IncludeImagesInChapters)
                {
                    volume.Chapters.ForEach(x => x.RemoveInserts());
                }
                chapters.AddRange(volume.Chapters.Where(filter));
            }

            if (volume.Gallery != null && filter(volume.Gallery))
            {
                var startGallery = volume.Gallery.GetChapter(true, Configuration.Options.Image.SplashImages == GallerySetting.Start, Configuration.Options.Image.ChapterImages == GallerySetting.Start);
                if (startGallery != null) chapters.Add(startGallery);

                var endGallery = volume.Gallery.GetChapter(false, Configuration.Options.Image.SplashImages == GallerySetting.End, Configuration.Options.Image.ChapterImages == GallerySetting.End);
                if (endGallery != null) chapters.Add(endGallery);
            }

            if (!Configuration.Options.Image.IncludeImagesInChapters)
            {
                volume.BonusChapters.ForEach(x => x.RemoveInserts());
            }
            switch (Configuration.Options.Chapter.BonusChapter)
            {
                case BonusChapterSetting.Chronological:
                    chapters.AddRange(volume.BonusChapters.Where(filter));
                    break;
                case BonusChapterSetting.EndOfBook:
                    chapters.AddRange(volume.BonusChapters.Where(filter));
                    break;
            }

            if (Configuration.Options.Chapter.MangaChapters != BonusChapterSetting.LeaveOut)
            {
                chapters.AddRange(volume.MangaChapters.Where(filter));
            }

            if (Configuration.Options.Extras.ComfyLifeChapters != ComfyLifeSetting.None && volume.ComfyLifeChapter != null && filter(volume.ComfyLifeChapter))
            {
                chapters.Add(volume.ComfyLifeChapter);
            }

            if ((Configuration.Options.Extras.CharacterSheets == CharacterSheets.All) && (volume.CharacterSheet != null) && filter(volume.CharacterSheet))
            {
                chapters.Add(volume.CharacterSheet);
            }
            else if ((Configuration.Options.Extras.CharacterSheets == CharacterSheets.PerPart) && (volume.CharacterSheet != null) && volume.CharacterSheet.PartSheet && filter(volume.CharacterSheet))
            {
                chapters.Add(volume.CharacterSheet);
            }

            if (Configuration.Options.Extras.Maps)
            {
                chapters.AddRange(volume.Maps.Where(filter));
            }

            if (volume.Afterword != null && Configuration.Options.Extras.Afterword != AfterwordSetting.None && filter(volume.Afterword))
            {
                chapters.Add(volume.Afterword);
            }

            if(Configuration.Options.Extras.Polls && volume.CharacterPoll != null && filter(volume.CharacterPoll))
            {
                chapters.Add(volume.CharacterPoll);
            }

            if (Configuration.Options.Collection.POVChapterCollection)
            {
                chapters.AddRange(volume.BonusChapters.Where(x => !string.IsNullOrWhiteSpace(x.POV)).Select(x => x.GetCollectionChapter()).Where(filter));
                chapters.AddRange(volume.POVChapters.Where(x => !string.IsNullOrWhiteSpace(x.POV)).Select(x => x.GetCollectionChapter()).Where(filter));
                chapters.AddRange(volume.MangaChapters.Where(x => !string.IsNullOrWhiteSpace(x.POV)).Select(x => x.GetCollectionChapter()).Where(filter));
            }
            chapters.AddRange(volume.POVChapters.Where(filter));

            return chapters;
        }
    }
}