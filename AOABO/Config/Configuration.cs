﻿using AOABO.Omnibus;
using Core.Downloads;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;


namespace AOABO.Config
{
    // copy/pasted from: https://stackoverflow.com/questions/2200241/in-c-sharp-how-do-i-define-my-own-exceptions
    [Serializable]
    public class CofigurationInitializtionException : Exception
    {
        // Constructors
        public CofigurationInitializtionException()
            : base("Failed to call Configuration.Initialize before using configuration method.")
        { }

        // Ensure Exception is Serializable
        protected CofigurationInitializtionException(SerializationInfo info, StreamingContext ctxt)
            : base(info, ctxt)
        { }
    }

    public static class Configuration
    {
        public const string defaultConfigFile = "options.json";
        public static readonly List<Volume> Volumes;
        public static readonly List<VolumeName> VolumeNames;
        public static readonly Dictionary<string, string> FolderNames;
        private static VolumeOptions Options { get; set; }
        private static bool isInitialized = false;
        private static string ConfigFilePath = string.Empty;

        static Configuration()
        {
            Volumes = new List<Volume>();
            ReloadVolumes();
            using (var reader = new StreamReader("JSON\\VolumeNames.json"))
            {
                DataContractJsonSerializer deserializer = new DataContractJsonSerializer(typeof(VolumeName[]));
                VolumeNames = (deserializer.ReadObject(reader.BaseStream) as VolumeName[])!.ToList();
            }

            using (var reader = new StreamReader("JSON\\folders.json"))
            {
                DataContractJsonSerializer deserializer = new DataContractJsonSerializer(typeof(FolderName[]));
                var list = (deserializer.ReadObject(reader.BaseStream) as FolderName[])!.ToList();

                FolderNames = list.ToDictionary(x => x.Name, x => x.Folder);
            }

        }

        public static void ReloadVolumes()
        {
            DataContractJsonSerializer deserializer = new DataContractJsonSerializer(typeof(Volume[]));
            Volumes.Clear();
            using (var reader = new StreamReader("JSON\\SideStories.json"))
            {
                Volumes.AddRange((deserializer.ReadObject(reader.BaseStream) as Volume[])!);
            }
            using (var reader = new StreamReader("JSON\\Fanbooks.json"))
            {
                Volumes.AddRange((deserializer.ReadObject(reader.BaseStream) as Volume[])!);
            }
            using (var reader = new StreamReader("JSON\\MangaP1.json"))
            {
                Volumes.AddRange((deserializer.ReadObject(reader.BaseStream) as Volume[])!);
            }
            using (var reader = new StreamReader("JSON\\MangaP2.json"))
            {
                Volumes.AddRange((deserializer.ReadObject(reader.BaseStream) as Volume[])!);
            }
            using (var reader = new StreamReader("JSON\\MangaP3.json"))
            {
                Volumes.AddRange((deserializer.ReadObject(reader.BaseStream) as Volume[])!);
            }
            using (var reader = new StreamReader("JSON\\MangaP4.json"))
            {
                Volumes.AddRange((deserializer.ReadObject(reader.BaseStream) as Volume[])!);
            }
            using (var reader = new StreamReader("JSON\\LNP1.json"))
            {
                Volumes.AddRange((deserializer.ReadObject(reader.BaseStream) as Volume[])!);
            }
            using (var reader = new StreamReader("JSON\\LNP2.json"))
            {
                Volumes.AddRange((deserializer.ReadObject(reader.BaseStream) as Volume[])!);
            }
            using (var reader = new StreamReader("JSON\\LNP3.json"))
            {
                Volumes.AddRange((deserializer.ReadObject(reader.BaseStream) as Volume[])!);
            }
            using (var reader = new StreamReader("JSON\\LNP4.json"))
            {
                Volumes.AddRange((deserializer.ReadObject(reader.BaseStream) as Volume[])!);
            }
            using (var reader = new StreamReader("JSON\\LNP5.json"))
            {
                Volumes.AddRange((deserializer.ReadObject(reader.BaseStream) as Volume[])!);
            }
            using (var reader = new StreamReader("JSON\\Hannelore.json"))
            {
                Volumes.AddRange((deserializer.ReadObject(reader.BaseStream) as Volume[])!);
            }
        }

        public static void Initialize(string configFile = defaultConfigFile)
        {
            if (!isInitialized)
            {
                Options = new VolumeOptions();

                if (File.Exists(configFile))
                {
                    using (var reader = new StreamReader(configFile))
                    {
                        var deserializer = new DataContractJsonSerializer(typeof(VolumeOptions));
                        Options = (VolumeOptions)deserializer.ReadObject(reader.BaseStream);
                        Options.Upgrade();
                    }
                }
                ConfigFilePath = configFile;
                isInitialized = true;
            }
        }

        // Band-aid needed to ensure that Options has been initialized before it is accessed
        // Set Options to private and access it through this accessor
        // TODO: Need to refactor somehow... TBD
        public static VolumeOptions Options_
        {
            get
            {
                if (!isInitialized)
                    throw new CofigurationInitializtionException();

                return Options;
            }
        }

        public static void UpdateOptions()
        {
            if (!isInitialized)
                throw new CofigurationInitializtionException();

            bool finished = false;
            while (!finished)
            {
                Console.Clear();

                Console.WriteLine("Which setting would you like to change?");
                Console.WriteLine($"0 - Omnibus Structure ({Options.OutputStructureSetting})");
                Console.WriteLine("1 - Chapter Settings");
                Console.WriteLine("2 - Image Settings");
                Console.WriteLine("3 - Extra Content Settings");
                Console.WriteLine($"4 - Human-Readable Internal Filenames ({Options.UseHumanReadableFileStructure})");
                Console.WriteLine("5 - Folder Settings");
                Console.WriteLine("Press any other key to return to main menu");

                var key = Console.ReadKey();
                switch (key.KeyChar)
                {
                    case '0':
                        SetStructure();
                        break;
                    case '1':
                        SetChapterSettings();
                        break;
                    case '2':
                        SetImageSettings();
                        break;
                    case '3':
                        SetExtraContentSettings();
                        break;
                    case '4':
                        SetBool("Use human-readable file names inside the .epub? (May cause issues with iBooks.)", x => Options.UseHumanReadableFileStructure = x);
                        break;
                    case '5':
                        SetFolderSettings();
                        break;
                    default:
                        finished = true;
                        break;
                }
            }

            PersistOptions();
        }

        public static void PersistOptions()
        {
            if (isInitialized)
            {
                if (File.Exists(ConfigFilePath))
                {
                    File.Delete(ConfigFilePath);
                }
                var serializer = new DataContractJsonSerializer(typeof(VolumeOptions));
                using (var stream = File.Open(ConfigFilePath, FileMode.Create))
                {
                    serializer.WriteObject(stream, Options);
                }
            }
        }

        private static void SetBool(string question, Action<bool> set)
        {
            Console.WriteLine();
            Console.WriteLine($"{question} Y/N");
            var key = Console.ReadKey();
            set(false);
            switch (key.KeyChar)
            {
                case 'y':
                case 'Y':
                    set(true);
                    break;
            }
        }

        private static void SetNullableInt(string question, string intQuestion, Action<int?> set, int? min, int? max)
        {
            Console.WriteLine();
            Console.WriteLine($"{question} Y/N");
            var key = Console.ReadKey();
            switch (key.KeyChar)
            {
                case 'y':
                case 'Y':
                    SetInt(intQuestion, x => set(x), min, max);
                    break;
                default:
                    set(null);
                    break;
            }
        }

        private static void SetInt(string question, Action<int> set, int? min, int? max)
        {
            while (true)
            {
                Console.WriteLine();
                Console.WriteLine($"{question}");
                var str = Console.ReadLine();
                if (int.TryParse(str, out var value))
                {
                    if ((!min.HasValue || min <= value) && (!max.HasValue || max >= value))
                    {
                        set(value);
                        break;
                    }
                }
            }
        }

        private static void SetChapterSettings()
        {
            if (!isInitialized)
                throw new CofigurationInitializtionException();

            while (true)
            {
                Console.Clear();
                Console.WriteLine($"0 - Include Regular Chapters (Currently {Options.Chapter.IncludeRegularChapters})");
                Console.WriteLine($"1 - Include Bonus Chapters (Currently {Options.Chapter.BonusChapterSetting})");
                Console.WriteLine($"2 - Include Manga Chapters (Currently {Options.Chapter.MangaChapterSetting})");
                Console.WriteLine($"3 - Update Chapter Headers to match the names in the index (Currently {Options.Chapter.UpdateChapterNames})");

                var key = Console.ReadKey();
                switch (key.KeyChar)
                {
                    case '0':
                        SetBool("Include Regular Chapters (Myne POV + Prologues and Epilogues)", x => Options.Chapter.IncludeRegularChapters = x);
                        break;
                    case '1':
                        Options.Chapter.BonusChapter = BonusChapterSetting.Chronological;
                        Console.WriteLine();
                        Console.WriteLine("0 - Place Bonus Chapters after the last chapter they overlap with.");
                        Console.WriteLine("1 - Place Bonus Chapters at the end of the Volume");
                        Console.WriteLine("2 - Leave out Bonus Chapters");
                        key = Console.ReadKey();
                        switch (key.KeyChar)
                        {
                            case '1':
                                Options.Chapter.BonusChapter = BonusChapterSetting.EndOfBook;
                                break;
                            case '2':
                                Options.Chapter.BonusChapter = BonusChapterSetting.LeaveOut;
                                break;
                        }
                        break;
                    case '2':
                        Options.Chapter.MangaChapters = BonusChapterSetting.Chronological;
                        Console.WriteLine();
                        Console.WriteLine("0 - Place Manga Chapters after the last chapter they overlap with.");
                        Console.WriteLine("1 - Place Manga Chapters at the end of the Volume");
                        Console.WriteLine("2 - Leave out Manga Chapters");
                        key = Console.ReadKey();
                        switch (key.KeyChar)
                        {
                            case '1':
                                Options.Chapter.MangaChapters = BonusChapterSetting.EndOfBook;
                                break;
                            case '2':
                                Options.Chapter.MangaChapters = BonusChapterSetting.LeaveOut;
                                break;
                        }
                        break;
                    case '3':
                        SetBool("Would you like the chapter headers updated to match their titles in the index?", x => Options.Chapter.UpdateChapterNames = x);
                        break;
                    default:
                        return;
                }
            }
        }

        private static void SetImageSettings()
        {
            if (!isInitialized)
                throw new CofigurationInitializtionException();

            while (true)
            {
                Console.Clear();
                Console.WriteLine($"0 - Include/Exclude Chapter Inserts (Currently {Options.Image.IncludeImagesInChaptersSetting})");
                Console.WriteLine($"1 - Bonus Image Gallery (Currently {Options.Image.SplashImagesSetting})");
                Console.WriteLine($"2 - Chapter Insert Gallery (Currently {Options.Image.ChapterImagesSetting})");
                Console.WriteLine($"3 - Set Maximum Image Width (Currently {Options.Image.MaxWidthSetting})");
                Console.WriteLine($"4 - Set Maximum Image Height (Currently {Options.Image.MaxHeightSetting})");
                Console.WriteLine($"5 - Set Resized Image Quality (Currently {Options.Image.Quality})");
                Console.WriteLine($"6 - Set Manga Quality (Currently {Options.Image.MangaQualitySetting})");
                var key = Console.ReadKey();
                switch (key.KeyChar)
                {
                    case '0':
                        SetBool("Include Chapter Insert Images", x => Options.Image.IncludeImagesInChapters = x);
                        break;
                    case '1':
                        Console.WriteLine();
                        Console.WriteLine("Which gallery do you want Bonus Images to be included in?");
                        Console.WriteLine("0 - The Start of each Volume.");
                        Console.WriteLine("1 - The End of each Volume.");
                        Console.WriteLine("2 - None");
                        Options.Image.SplashImages = GallerySetting.Start;
                        key = Console.ReadKey();
                        switch (key.KeyChar)
                        {
                            case '1':
                                Options.Image.SplashImages = GallerySetting.End;
                                break;
                            case '2':
                                Options.Image.SplashImages = GallerySetting.None;
                                break;
                        }
                        break;
                    case '2':
                        Console.WriteLine();
                        Console.WriteLine("Which gallery do you want Chapter Inserts to be included in?");
                        Console.WriteLine("0 - The Start of each Volume.");
                        Console.WriteLine("1 - The End of each Volume.");
                        Console.WriteLine("2 - None");
                        Options.Image.ChapterImages = GallerySetting.Start;
                        key = Console.ReadKey();
                        switch (key.KeyChar)
                        {
                            case '1':
                                Options.Image.ChapterImages = GallerySetting.End;
                                break;
                            case '2':
                                Options.Image.ChapterImages = GallerySetting.None;
                                break;
                        }
                        break;
                    case '3':
                        SetNullableInt("Do you want to enforce a maximum image width?", "How many pixels wide should the limit be?", x => Options.Image.MaxWidth = x, 1, null);
                        break;
                    case '4':
                        SetNullableInt("Do you want to enforce a maximum image height?", "How many pixels tall should the limit be?", x => Options.Image.MaxHeight = x, 1, null);
                        break;
                    case '5':
                        SetInt("Pick a new Image Quality (1-100, higher numbers produce better images and larger file sizes)", x => Options.Image.Quality = x, 1, 100);
                        break;
                    case '6':
                        Console.WriteLine("Which manga version do you want to download?");
                        Console.WriteLine("1 - Mobile");
                        Console.WriteLine("2 - Desktop");
                        Console.WriteLine("3 - 4k");
                        key = Console.ReadKey();

                        switch (key.KeyChar)
                        {
                            case '1':
                                Options.Image.MangaQuality = MangaQuality.Mobile;
                                break;
                            case '2':
                                Options.Image.MangaQuality = MangaQuality.Desktop;
                                break;
                            case '3':
                                Options.Image.MangaQuality = MangaQuality.FourK;
                                break;
                        }
                        break;
                    default:
                        return;
                }
            }
        }

        public static void SetFolders(string? inputFolder, string? outputFolder)
        {
            if (!isInitialized)
                throw new CofigurationInitializtionException();

            if (!string.IsNullOrEmpty(inputFolder))
            {
                Options.Folder.InputFolder = inputFolder;
            }

            if (!string.IsNullOrEmpty(outputFolder))
            {
                Options.Folder.OutputFolder = outputFolder;
            }
        }



        private static void SetFolderSettings()
        {
            if (!isInitialized)
                throw new CofigurationInitializtionException();

            while (true)
            {
                Console.Clear();
                Console.WriteLine($"0 - Input Folder (Currently {Options.Folder.InputFolderSetting})");
                Console.WriteLine($"1 - Output Folder (Currently {Options.Folder.OutputFolderSetting})");
                Console.WriteLine($"2 - Delete the Temporary Folder once the omnibus is built? ({Options.Folder.DeleteTempFolder})");

                var key = Console.ReadKey();
                switch (key.KeyChar)
                {
                    case '0':
                        Console.WriteLine();
                        Console.WriteLine("Enter the new input folder.");
                        Console.WriteLine("This can be absolute or relative, leave blank for 'the folder this program is in'.");
                        Options.Folder.InputFolder = Console.ReadLine() ?? string.Empty;
                        break;
                    case '1':
                        Console.WriteLine();
                        Console.WriteLine("Enter the new output folder.");
                        Console.WriteLine("This can be absolute or relative, leave blank for 'the folder this program is in'.");
                        Options.Folder.OutputFolder = Console.ReadLine() ?? string.Empty;
                        break;
                    case '2':
                        SetBool("Delete the Temporary Folder once the omnibus is built?", x => Options.Folder.DeleteTempFolder = x);
                        break;
                    default:
                        return;
                }
            }
        }

        private static void SetExtraContentSettings()
        {
            if (!isInitialized)
                throw new CofigurationInitializtionException();

            while (true)
            {
                Console.Clear();
                Console.WriteLine($"0 - Include Comfy Life Chapters (Currently {Options.Extras.ComfyLifeChaptersSetting})");
                Console.WriteLine($"1 - Include Character Sheets ({Options.Extras.CharacterSheetsSetting})");
                Console.WriteLine($"2 - Include Maps ({Options.Extras.Maps})");
                Console.WriteLine($"3 - Include Afterwords ({Options.Extras.AfterwordSetting})");
                Console.WriteLine($"4 - Include Polls ({Options.Extras.Polls})");
                Console.WriteLine($"5 - Include POV Chapter collection ({Options.Collection.POVChapterOrderingSetting})");

                var key = Console.ReadKey();
                switch (key.KeyChar)
                {
                    case '0':
                        Options.Extras.ComfyLifeChapters = ComfyLifeSetting.VolumeEnd;
                        Console.WriteLine();
                        Console.WriteLine("0 - Place Comfy Life Chapters after the volume they were published with.");
                        Console.WriteLine("1 - Place Comfy Life Chapters at the end of the omnibus.");
                        Console.WriteLine("2 - Leave out Comfy Life Chapters");
                        key = Console.ReadKey();
                        switch (key.KeyChar)
                        {
                            case '1':
                                Options.Extras.ComfyLifeChapters = ComfyLifeSetting.OmnibusEnd;
                                break;
                            case '2':
                                Options.Extras.ComfyLifeChapters = ComfyLifeSetting.None;
                                break;
                        }
                        break;
                    case '1':
                        Console.WriteLine();
                        Console.WriteLine("How many Character Sheets do you want included?");
                        Console.WriteLine("0 - All of them.");
                        Console.WriteLine("1 - Last one in each part.");
                        Console.WriteLine("2 - None");
                        Options.Extras.CharacterSheets = CharacterSheets.PerPart;
                        key = Console.ReadKey();
                        switch (key.KeyChar)
                        {
                            case '0':
                                Options.Extras.CharacterSheets = CharacterSheets.All;
                                break;
                            case '2':
                                Options.Extras.CharacterSheets = CharacterSheets.None;
                                break;
                        }
                        break;
                    case '2':
                        SetBool("Would you like to include maps?", x => Options.Extras.Maps = x);
                        break;
                    case '3':
                        Console.WriteLine();
                        Options.Extras.Afterword = AfterwordSetting.None;
                        Console.WriteLine("0 - Exclude Afterwords");
                        Console.WriteLine("1 - Include Afterwords at the end of each volume");
                        Console.WriteLine("2 - Include Afterwords at the end of the Omnibus");
                        key = Console.ReadKey();
                        switch (key.KeyChar)
                        {
                            case '1':
                                Options.Extras.Afterword = AfterwordSetting.VolumeEnd;
                                break;
                            case '2':
                                Options.Extras.Afterword = AfterwordSetting.OmnibusEnd;
                                break;
                        }
                        break;
                    case '4':
                        SetBool("Do you want to include the Character Polls?", x => Options.Extras.Polls = x);
                        break;
                    case '5':
                        SetBool("Do you want to include a collection of the POV chapters?", x => Options.Collection.POVChapterCollection = x);
                        if (Options.Collection.POVChapterCollection)
                        {
                            SetBool("Do you want the POV chapters ordered by POV character?", x => Options.Collection.POVChapterOrdering = x);
                        }
                        break;
                    default:
                        return;
                }
            }
        }

        private static void SetStructure()
        {
            if (!isInitialized)
                throw new CofigurationInitializtionException();

            Options.OutputStructure = OutputStructure.Flat;
            Console.WriteLine();
            Console.WriteLine("How should the output file be structured?");
            Console.WriteLine("0: Flat structure");
            Console.WriteLine("1: By Part");
            Console.WriteLine("2: By Part and Volume");
            Console.WriteLine("3: By Season");
            var key = Console.ReadKey();
            Console.WriteLine();
            IFolder folder = new BasicFolder();
            switch (key.KeyChar)
            {
                case '1':
                    Options.OutputStructure = OutputStructure.Parts;
                    break;
                case '2':
                    Options.OutputStructure = OutputStructure.Volumes;
                    break;
                case '3':
                    Options.OutputStructure = OutputStructure.Seasons;
                    Console.WriteLine();
                    Options.StartYear = -100;
                    while (Options.StartYear == -100)
                    {
                        Console.WriteLine("Which year should be used for the story beginning (Myne is 5 at the start of P1V1)?");
                        var yearinput = Console.ReadLine();
                        if (int.TryParse(yearinput, out var y))
                            Options.StartYear = y;
                    }

                    Console.WriteLine("How would you like the years formatted? (Entering nothing will give you plain numbers. To give actual labels, enter any text with a 0 where you want the year to be.)");
                    Console.WriteLine("0 - XX");
                    Console.WriteLine("1 - Year XX");

                    var subKey = Console.ReadKey();
                    switch (subKey.KeyChar)
                    {
                        case '1':
                            Options.OutputYearFormat = 1;
                            break;
                        default:
                            Options.OutputYearFormat = 0;
                            break;
                    }
                    break;
            }
        }
    }
}