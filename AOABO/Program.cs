using AOABO.Chapters;
using AOABO.Config;
using AOABO.OCR;
using AOABO.Omnibus;
using Core;
using Core.Downloads;
using System;
using System.CommandLine;
using System.Linq.Expressions;
using System.Reflection;

#if DEBUG
using System.Text;
using System.Text.Json;
using Windows.Gaming.Input;
using Windows.Globalization.DateTimeFormatting;
using Windows.Media.Audio;
using static AOABConsole;
using static AOABO.Config.VolumeOptions;
#endif


static class AOABConsole
{
    public const string interactiveDescription = "Run interactively. [default behavior]";
    public const string createDescription = "Create an Ascendance of a Bookworm Omnibus";
    public const string updateDescription = "Update Omnibus Creation Settings";
    public const string loginDescription   = "Set Login Details";
    public const string downloadDescription = "Download Updates";
    public const string ocrDescription = "OCR Manga Bonus Written Chapters";
    public const string invalidCredentials = "Unable to validate credentials";

    //public enum Structure : int
    //{
    //    Flat = 0,
    //    ByPart = 1,
    //    ByPartAndVolume = 2,
    //    BySeason = 3
    //}

    static void SetIfNotNull<T>(Action<T> doStorage, T? value) where T: unmanaged
    {
        if (value != null)
        {
            doStorage((T)value);
        }
    }

    static void SetIfNotNull<T>(Action<T> doStorage, T? value) where T : class
    {
        if (value != null)
        {
            doStorage((T)value);
        }
    }

    public static void MaybeSet<Obj, T>(this Obj? obj, Expression<Func<Obj, T>> expr, T? value) where Obj : class
    {
        if (obj != null && value != null)
        {
            if (expr.Body is MemberExpression { Member: PropertyInfo prop })
            {
                prop.SetValue(obj, value);
            }
        }
    }


    public enum ImageLimit
    {
        NoLimit,
        LimitWidth,
        LimitHeight,
        LimitWidthAndHeight
    }

    public enum OmnibusScope : int
    {  
        EntireSeries = 0,
        Part1 = 1,
        Part2 = 2,
        Part3 = 3,
        Part4 = 4,
        Part5 = 5,
        Fanbooks = 6
    };

    private static string normalizeFolder(string? folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath)) // if empty string, use CWD
        {
            return Directory.GetCurrentDirectory();
        }
        else if (folderPath.Length > 1 && folderPath[1].Equals(':')) // if starts with drive letter, assume it is an absolute path
        {
            return folderPath;
        }
        else
        {
            return Path.Join(Directory.GetCurrentDirectory(), folderPath);
        }
    }

    private static string normalizeFile(string filePath)
    {
        return Path.Join(normalizeFolder(Path.GetDirectoryName(filePath)), Path.GetFileName(filePath));
    }

    static async Task<int> Main(string[] args)
    {
        // TODO: Clean up arguments - remove "no-*" arguements


        int returnCode = 0;
        HttpClient client = new HttpClient();
        var rootCommand = new RootCommand("Ascendance of a Bookworm Omnibus Creator");


        // Global options //////////////////////////////////////////////////////////////////////////////
        Option<string> configFileOption  = new (name: "--config",
                                                description: "Configuration file path",
                                                getDefaultValue: () => Configuration.defaultConfigFile);
        Option<string> accountFileOption = new (name: "--account",
                                                description: "Account file path",
                                                getDefaultValue: () => Login.defaultAccountFile);
        Option<string> inputFolder       = new (name: "--input",
                                                description: "The Source folder - where ebooks will be downloaded and used for generating the omnibus",
                                                getDefaultValue: () => string.Empty);
        Option<string> outputFolder      = new (name: "--output",
                                                description: "The destination folder - where the omnibus will be generated",
                                                getDefaultValue: () => string.Empty );
        rootCommand.AddGlobalOption(configFileOption);
        rootCommand.AddGlobalOption(accountFileOption);
        rootCommand.AddGlobalOption(inputFolder);
        rootCommand.AddGlobalOption(outputFolder);


        // default behavior if no argument provided //////////////////////////////////////////////////////////////
        rootCommand.SetHandler(async (config, account) =>
            {
                await RunInteractive(normalizeFile(config), normalizeFile(account), client);
            },
            configFileOption, accountFileOption);

        
        // interactive Command ////////////////////////////////////////////////////////////////////////////////////////
        var interactiveCommand = new Command("interactive", interactiveDescription);
        rootCommand.AddCommand(interactiveCommand);
        interactiveCommand.SetHandler(async (config, account) =>
            {
                await RunInteractive(normalizeFile(config), normalizeFile(account), client);
            },
            configFileOption, accountFileOption);


        // create command ///////////////////////////////////////////////////////////////////////////////////////////
        Option<OmnibusScope> omnibusPart = new (name: "--part",
                                                description: "Parts to include in the omnibus creation",
                                                getDefaultValue: () => OmnibusScope.EntireSeries);
        var createCommand = new Command("create", createDescription)
        {
            omnibusPart
        };
        rootCommand.AddCommand(createCommand);
        createCommand.SetHandler(async (config, input, output, part) =>      
            {
                Configuration.Initialize(normalizeFile(config));
                Configuration.SetFolders(normalizeFolder(input), normalizeFolder(output));
                Configuration.PersistOptions();
                await OmnibusBuilder.BuildOmnibus(Convert.ToString(part)![0]);
            }, 
            configFileOption, inputFolder, outputFolder, omnibusPart);

        // Login Command ///////////////////////////////////////////////////////////////////////////////////////////
        var loginCommand = new Command("login", loginDescription);
        rootCommand.AddCommand(loginCommand);
        loginCommand.SetHandler(async (account) =>
            {
                string username = Login.ConsoleGetUsername();
                string password = Login.ConsoleGetPassword();
                var login = await Login.CreateLogin(username, password, client);
                if (login != null)
                {
                    await Login.PersistLoginInfo(normalizeFile(account), username, password);
                }
                else 
                {
                    System.Console.WriteLine(invalidCredentials);
                    returnCode = 1;
                }
            },
            accountFileOption);


        // Download Command ////////////////////////////////////////////////////////////////////////////////////////
        Option<bool> skipUpdateFiles = new (name: "--no-update",
                                            description: "Do not download books updated books",
                                            getDefaultValue: () => false);
        var downloadCommand = new Command("download", downloadDescription)
        {
            skipUpdateFiles
        };
        rootCommand.AddCommand(downloadCommand);
        downloadCommand.SetHandler(async (config, account, input, output, skipUpdate) =>
            {
                var login = await Login.FromFile(account, client);
                if (login != null)
                {
                    Configuration.Initialize(normalizeFile(config));
                    Configuration.SetFolders(normalizeFolder(input), normalizeFolder(output));
                    Configuration.PersistOptions();
                    await Downloader.DoDownloads(client, 
                                                 login.AccessToken,
                                                 Configuration.Options_.Folder.InputFolder, 
                                                 Configuration.VolumeNames.Select(x => new Name { ApiSlug = x.ApiSlug, FileName = x.FileName, Quality = x.Quality! }),
                                                 Configuration.Options_.Image.MangaQuality,
                                                 (string _) => skipUpdate);

                }
                else
                {
                    Console.WriteLine(invalidCredentials);
                    returnCode = 1;
                }
            },
            configFileOption, accountFileOption, inputFolder, outputFolder, skipUpdateFiles);

        
        // OCR command /////////////////////////////////////////////////////////////////////////////////
        var ocrCommand = new Command("ocr", ocrDescription);
        rootCommand.AddCommand(ocrCommand);
        ocrCommand.SetHandler(async (config, account, input) =>
            {
                var login = await Login.FromFile(account, client);
                if (login != null)
                {
                    Configuration.Initialize(normalizeFile(config));
                    Configuration.SetFolders(normalizeFolder(input), null);
                    Configuration.PersistOptions();
                    await OCR.BuildOCROverrides(login, client);
                }
                else
                {
                    Console.WriteLine(invalidCredentials);
                    returnCode = 1;
                }
            },
            configFileOption, accountFileOption, inputFolder);


        // Update command //////////////////////////////////////////////////////////////////////////////

        //--structure 0/1/2/3 [0] -- 0: Flat structure, 1: By Part, 2: By Part and Volume, 3: By Season

        Option<OutputStructure?> optionStructure = new (name: "--structure",
                                                        description: "How the omnibus should be structured");
        //--structure-start-year [5]
        Option<int?> optionBySeasonStartYear = new (name: "--start-year",
                                                    description: $"When --structure={OutputStructure.Seasons.ToString()}, the year that should be used for the story beginning (Myne is 5 at the start of P1V1)");

        //--structure-year-format 0/1 [1]
        Option<int?> optionBySeasonYearFormat = new (name: "--year-format",
                                                     description: $"When --structure={OutputStructure.Seasons.ToString()}, how to format the year\n [0] XX\n [1] Year XX\n");

        //--chapter-include-regular 0/1/2 [0]
        Option<bool?> optionIncludeRegular = new (name:"--no-include-regular-chapters",
                                                  description: "Do not include regular Chapters (Myne POV + Prologues and Epilogues)");

        //--chapter-include-bonus-chapters 0/1/2 [0]
        Option<BonusChapterSetting?> optionIncludeBonus = new (name: "--include-bonus-chapters",
                                                               description: "Include Bonus Chapters");

        //--chapter-include-manga-chapters
        Option<BonusChapterSetting?> optionIncludeManga = new ( name: "--include-manga-chapters",
                                                                description: "Include Manga Chapters");
        //--chapter-match-header [True]
        Option<bool?> optionMatchHeader = new (name:"--match-headers",
                                               description: "Do not update chapter headers to match the names in the index");



        //--image-chapter-inserts [True]
        Option<bool?> optionChapterInserts = new ( name: "--insert-images",
                                                   description: "Skip chapter insert images");

        //--image-bonus-image-gallery 0/1/2 [2]    0 - The Start of each Volume, 1 - The End of each Volume, 2 - None
        Option<GallerySetting?> optionBonusImageGallery = new (name: "--bonus-image-gallery",
                                                               description: "Include a bonus image gallery");

        //--image-chapter-insert-gallery    [2]    0 - The Start of each Volume, 1 - The End of each Volume, 2 - None
        Option<GallerySetting?> optionInsertImageGallery = new (name: "--insert-image-gallery",
                                                                description: "Include chapter insert gallery");

        Option<ImageLimit?> optionLimitImageSize = new (name: "--limit-image-size",
                                                        description: "Indictate whether image size should be limited. Use --maximumWidth and --maximumHeight to set the size limit",
                                                        getDefaultValue: () => ImageLimit.NoLimit);

        //--image-set-maximum-width [null]
        Option<int?> optionMaximumWidth = new (name: "--maximum-width",
                                               description: "Set the maximum image width in pixels");
        //--image-set-maximum-height [null]
        Option<int?> optionMaximumHeight = new (name: "--maximum-height",
                                                description: "Set the maximum image height in pixels");

        //--image-set-image-quality [90]
        Option<int?> optionImageQuality = new (name: "--image-quality",
                                               description: "Set resized image quality as a percentage");

        //--manga-quality 1/2/3 [3]
        Option<MangaQuality?> optionMangaQuality = new (name: "--manga-quality",
                                                        description: "Set manga download quality (more quality means larger file)");

        //--extra-comfy-life 0/1/2 [2]   0 - Place Comfy Life Chapters after the volume they were published with, 1 - Place Comfy Life Chapters at the end of the omnibus, 2 - Leave out Comfy Life Chapters
        Option<ComfyLifeSetting?> optionComfyLife = new (name: "--comfy-life",
                                                         description: "Include Comfy Life chapters");

        //--extra-character-sheets 0/1/2 0 - All of them.  1 - Last one in each part.  2 - None
        Option<CharacterSheets?> optionCharacterSheets = new (name: "--character-sheets",
                                                              description: "Include character sheets");
        //--extra-maps [True]
        Option<bool?> optionMaps = new (name: "--no-maps",
                                        description: "Do not include maps");

        //--extra-afterwords 0/1/2    0 - Exclude Afterwords, 1 - Include Afterwords at the end of each volume, 2 - Include Afterwords at the end of the Omnibus
        Option<AfterwordSetting?> optionAfterwords = new (name: "--afterwords",
                                                          description: "Include afterwords");
        //--extra-polls [True]
        Option<bool?> optionPolls = new (name: "--no-character-polls",
                                         description: "Skip the character polls");

        //--extra-pov-collection [False]
        Option<bool?> optionPovCollection = new (name:"--pov-collection",
                                                 description: "Include a collection of POV chapters");
        //--extra-pot-collection-order
        Option<bool?> optionPovCollectionOrder = new (name: "--pov-collection-no-order-by-character",
                                                      description: "When --pov-collection, do not order the POV collection chapter by character");


        //--readable-internal-filenames [False]
        Option<bool?> optionReadableFilenames = new (name: "--readable-filenames",
                                                     description: "Use human-readable internal filenames (May cause issues with iBooks)");
        //--folder-delete-temp [True]
        Option<bool?> optionDeleteTemp = new (name: "--no-delete-temp-folder",
                                              description: "Delete temporary folder once the omnibus is built");


        var updateCommand = new Command("update", updateDescription)
        {
            optionStructure,
            optionBySeasonStartYear,
            optionBySeasonYearFormat,
            optionIncludeRegular,
            optionIncludeBonus,
            optionIncludeManga,
            optionMatchHeader,
            optionChapterInserts,
            optionBonusImageGallery,
            optionInsertImageGallery,
            optionLimitImageSize,
            optionMaximumWidth,
            optionMaximumHeight,
            optionImageQuality,
            optionMangaQuality,
            optionComfyLife,
            optionCharacterSheets,
            optionMaps,
            optionAfterwords,
            optionPolls,
            optionPovCollection,
            optionPovCollectionOrder,
            optionReadableFilenames,
            optionDeleteTemp
        };
        rootCommand.AddCommand(updateCommand);
        updateCommand.SetHandler((context) =>
            {
                string config = context.ParseResult.GetValueForOption(configFileOption)!;
                string input = context.ParseResult.GetValueForOption(inputFolder)!;
                string output = context.ParseResult.GetValueForOption(outputFolder)!;

                var structure = context.ParseResult.GetValueForOption(optionStructure);
                var startYear = context.ParseResult.GetValueForOption(optionBySeasonStartYear);
                var yearFormat = context.ParseResult.GetValueForOption(optionBySeasonYearFormat);
                var includeRegular = context.ParseResult.GetValueForOption(optionIncludeRegular);
                var includeBonus = context.ParseResult.GetValueForOption(optionIncludeBonus);
                var includeManga = context.ParseResult.GetValueForOption(optionIncludeManga);
                var matchHeader = context.ParseResult.GetValueForOption(optionMatchHeader);
                var chapterInserts = context.ParseResult.GetValueForOption(optionChapterInserts);
                var bonusImageGallery = context.ParseResult.GetValueForOption(optionBonusImageGallery);
                var insertImageGallery = context.ParseResult.GetValueForOption(optionInsertImageGallery);
                var limitImageSize = context.ParseResult.GetValueForOption(optionLimitImageSize); // TODO: Add option
                var maxWidth = context.ParseResult.GetValueForOption(optionMaximumWidth);
                var maxHeight = context.ParseResult.GetValueForOption(optionMaximumHeight);
                var imageQuality = context.ParseResult.GetValueForOption(optionImageQuality);
                var mangaQuality = context.ParseResult.GetValueForOption(optionMangaQuality);
                var comfyLife = context.ParseResult.GetValueForOption(optionComfyLife);
                var characterSheets = context.ParseResult.GetValueForOption(optionCharacterSheets);
                var maps = context.ParseResult.GetValueForOption(optionMaps);
                var afterwords = context.ParseResult.GetValueForOption(optionAfterwords);
                var polls = context.ParseResult.GetValueForOption(optionPolls);
                var povCollection = context.ParseResult.GetValueForOption(optionPovCollection);
                var povCollectionOrder = context.ParseResult.GetValueForOption(optionPovCollectionOrder);
                var readableFilenames = context.ParseResult.GetValueForOption(optionReadableFilenames);
                var deleteTemp = context.ParseResult.GetValueForOption(optionDeleteTemp);

                Configuration.Initialize(normalizeFile(config));
                Configuration.SetFolders(normalizeFolder(input), normalizeFolder(output));


                //SetIfNotNull(x => options.OutputStructure = x, structure);
                Configuration.Options_.MaybeSet(x => x.OutputStructure, structure);
                Configuration.Options_.MaybeSet(x => x.StartYear, startYear);
                Configuration.Options_.MaybeSet(x => x.OutputYearFormat, yearFormat);
                Configuration.Options_.MaybeSet(x => x.IncludeRegularChapters, includeRegular);
                Configuration.Options_.MaybeSet(x => x.BonusChapterSetting, includeBonus);
                Configuration.Options_.MaybeSet(x => x.MangaChapters, includeManga);
                Configuration.Options_.MaybeSet(x => x.UpdateChapterNames, matchHeader);
                Configuration.Options_.MaybeSet(x => x.Image.IncludeImagesInChapters, chapterInserts);
                Configuration.Options_.MaybeSet(x => x.Image.SplashImages, bonusImageGallery);
                Configuration.Options_.MaybeSet(x => x.Image.ChapterImages, insertImageGallery);
                Configuration.Options_.MaybeSet(x => x.Image.MaxWidth, maxWidth);
                Configuration.Options_.MaybeSet(x => x.Image.MaxHeight, maxHeight);
                Configuration.Options_.MaybeSet(x => x.Extras.ComfyLifeChapters,comfyLife);
                Configuration.Options_.MaybeSet(x => x.Extras.CharacterSheets, characterSheets);
                Configuration.Options_.MaybeSet(x => x.Extras.Maps, maps);
                Configuration.Options_.MaybeSet(x => x.Extras.Afterword, afterwords);
                Configuration.Options_.MaybeSet(x => x.Extras.Polls, polls);
                Configuration.Options_.MaybeSet(x => x.Collection.POVChapterCollection, povCollection);
                Configuration.Options_.MaybeSet(x => x.Collection.POVChapterOrdering, povCollectionOrder);
                Configuration.Options_.MaybeSet(x => x.UseHumanReadableFileStructure, readableFilenames);
                Configuration.Options_.MaybeSet(x => x.Folder.DeleteTempFolder, deleteTemp);

                Configuration.PersistOptions();

            });



        await rootCommand.InvokeAsync(args);

        return returnCode;
    }

    public static async Task RunInteractive(string configFile, string accountFile, HttpClient client)
    {
        var executing = true;

        Configuration.Initialize(configFile);
        var login = await Login.FromFile(accountFile, client);

        while (executing)
        {
            Console.Clear();

            Console.WriteLine($"1 - {createDescription}");
            Console.WriteLine($"2 - {updateDescription}");
            Console.WriteLine($"3 - {loginDescription}");

            if (login != null)
            {
                Console.WriteLine($"4 - {downloadDescription}");
                Console.WriteLine($"5 - {ocrDescription}");
            }

#if DEBUG
            Console.WriteLine("6 - Redo JSON Files");
            Console.WriteLine("7 - Add Bonus Chapter");
            Console.WriteLine("8 - Create Tables");
#endif

            var key = Console.ReadKey();

            switch (key.KeyChar, login != null)
            {
                case ('1', true):
                case ('1', false):
                    await OmnibusBuilder.InteractivelyBuildOmnibus();
                    break;
                case ('2', true):
                case ('2', false):
                    Configuration.UpdateOptions();
                    break;
                case ('3', true):
                case ('3', false):
                    login = await Login.FromConsole(accountFile, client);
                    break;
                case ('4', true):
                    var inputFolder = normalizeFolder(Configuration.Options_.Folder.InputFolder);
                    await Downloader.DoDownloadsInteractive(client, login!.AccessToken, inputFolder, Configuration.VolumeNames.Select(x => new Name { ApiSlug = x.ApiSlug, FileName = x.FileName, Quality = x.Quality! }), Configuration.Options_.Image.MangaQuality);
                    break;
                case ('5', true):
                    await OCR.BuildOCROverrides(login!, client);
                    break;
#if DEBUG
                case ('6', true):
                case ('6', false):
                    await RedoJSON();
                    break;
                case ('7', true):
                case ('7', false):
                    await AddChapter();
                    break;
                case ('8', true):
                case ('8', false):
                    await CreateTables();
                    break;
#endif
                default:
                    executing = false;
                    break;
            }
        }
    }

#if DEBUG
    private static async Task RedoJSON()
    {
        var chapters = Configuration.Volumes.SelectMany(x =>
        {
            var c = new List<Chapter>();
            c.AddRange(x.POVChapters);
            c.AddRange(x.MangaChapters);
            c.AddRange(x.BonusChapters);
            c.AddRange(x.Chapters);
            if (x.CharacterSheet != null) c.Add(x.CharacterSheet);
            if (x.ComfyLifeChapter != null) c.Add(x.ComfyLifeChapter);
            return c;
        }).GroupBy(x => x.Volume).ToDictionary(x => x.Key, x => x.ToArray());

        foreach (var set in chapters)
        {
            var i = 1;
            foreach (var chapter in set.Value.OrderBy(x => (x is MoveableChapter xx) ? xx.EarlySortOrder : x.SortOrder).ToArray())
            {
                if (chapter is MoveableChapter c)
                {
                    c.EarlySortOrder = $"{set.Key}{i:00}";
                }
                else
                {
                    chapter.SortOrder = $"{set.Key}{i:00}";
                }
                i++;
            }
        }

        var bonusChapters = Configuration.Volumes.SelectMany(x => x.BonusChapters.Union(x.MangaChapters)).GroupBy(x => x.Volume).ToDictionary(x => x.Key, x => x.ToArray());

        foreach (var set in bonusChapters)
        {
            var i = 1;
            foreach (var chapter in set.Value.OrderBy(x => (x is MoveableChapter xx) ? xx.EarlySortOrder : x.SortOrder))
            {
                chapter.LateSortOrder = $"{set.Key}96{i:00}";
                i++;
            }
        }

        await SaveAll();
    }

    private static async Task SaveAll()
    {
        var fanbooks = new List<Volume>();
        var p1 = new List<Volume>();
        var p2 = new List<Volume>();
        var p3 = new List<Volume>();
        var p4 = new List<Volume>();
        var p5 = new List<Volume>();
        var mp1 = new List<Volume>();
        var mp2 = new List<Volume>();
        var mp3 = new List<Volume>();
        var mp4 = new List<Volume>();
        var ss = new List<Volume>();
        var han = new List<Volume>();

        foreach (var vol in Configuration.Volumes)
        {
            switch (vol.InternalName)
            {
                case "FB1":
                case "FB2":
                case "FB3":
                case "FB4":
                case "FB5":
                case "FB6":
                    fanbooks.Add(vol);
                    break;
                case "LN0101":
                case "LN0102":
                case "LN0103":
                    p1.Add(vol);
                    break;
                case "LN0201":
                case "LN0202":
                case "LN0203":
                case "LN0204":
                    p2.Add(vol);
                    break;
                case "LN0301":
                case "LN0302":
                case "LN0303":
                case "LN0304":
                case "LN0305":
                    p3.Add(vol);
                    break;
                case "LN0401":
                case "LN0402":
                case "LN0403":
                case "LN0404":
                case "LN0405":
                case "LN0406":
                case "LN0407":
                case "LN0408":
                case "LN0409":
                    p4.Add(vol);
                    break;
                case "LN0501":
                case "LN0502":
                case "LN0503":
                case "LN0504":
                case "LN0505":
                case "LN0506":
                case "LN0507":
                case "LN0508":
                case "LN0509":
                case "LN0510":
                case "LN0511":
                case "LN0512":
                    p5.Add(vol);
                    break;
                case "M0101":
                case "M0102":
                case "M0103":
                case "M0104":
                case "M0105":
                case "M0106":
                case "M0107":
                    mp1.Add(vol);
                    break;
                case "M0201":
                case "M0202":
                case "M0203":
                case "M0204":
                case "M0205":
                case "M0206":
                case "M0207":
                case "M0208":
                case "M0209":
                    mp2.Add(vol);
                    break;
                case "M0301":
                case "M0302":
                case "M0303":
                case "M0304":
                    mp3.Add(vol);
                    break;
                case "RAS1":
                case "SS01":
                case "SS02":
                    ss.Add(vol);
                    break;
                case "M0401":
                case "M0402":
                case "M0403":
                case "M0404":
                    mp4.Add(vol);
                    break;
                case "0601":
                    han.Add(vol);
                    break;
            }
        }

        await Task.WhenAll(
            Save("Fanbooks", fanbooks),
            Save("LNP1", p1),
            Save("LNP2", p2),
            Save("LNP3", p3),
            Save("LNP4", p4),
            Save("LNP5", p5),
            Save("MangaP1", mp1),
            Save("MangaP2", mp2),
            Save("MangaP3", mp3),
            Save("SideStories", ss),
            Save("MangaP4", mp4),
            Save("Hannelore", han));
    }

    private static async Task Save(string filename, List<Volume> vols)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        List<Task> tasks = new();
        using (var writer = new StreamWriter($"JSON\\{filename}.json"))
        {
            tasks.Add(JsonSerializer.SerializeAsync(writer.BaseStream, vols, options: options));
        }
        using (var writer = new StreamWriter($"..\\..\\..\\JSON\\{filename}.json"))
        {
            tasks.Add(JsonSerializer.SerializeAsync(writer.BaseStream, vols, options: options));
        }

        await Task.WhenAll(tasks);
    }

    private static async Task AddChapter()
    {
        Console.Clear();
        Console.WriteLine("Source Book:");

        var bookCode = Console.ReadLine();

        var book = Configuration.Volumes.FirstOrDefault(x => x.InternalName.Equals(bookCode));

        if (book == null) return;

        Console.WriteLine("Volume:");
        var vol = Console.ReadLine();
        var volume = Configuration.Volumes.FirstOrDefault(x => x.InternalName.Equals($"LN{vol}"));
        var chapters = Configuration.Volumes.SelectMany(x =>
        {
            var c = new List<Chapter>();
            c.AddRange(x.POVChapters);
            c.AddRange(x.MangaChapters);
            c.AddRange(x.BonusChapters);
            c.AddRange(x.Chapters);
            if (x.CharacterSheet != null) c.Add(x.CharacterSheet);
            if (x.ComfyLifeChapter != null) c.Add(x.ComfyLifeChapter);
            return c;
        }).Where(x => x.Volume.Equals(vol)).ToArray();

        if (volume == null) return;
        var epilogue = volume.POVChapters.FirstOrDefault(x => x.ChapterName.Equals("Epilogue"));
        if (epilogue == null) return;

        var chapter = new BonusChapter
        {
            LateSeason = epilogue.Season,
            LateYear = epilogue.Year,
            OriginalFilenames = new List<string>(),
            Volume = vol ?? string.Empty
        };

        Console.WriteLine("Chapter Source:");
        chapter.Source = Console.ReadLine() ?? string.Empty;

        Console.WriteLine("Chapter Name:");
        chapter.ChapterName = Console.ReadLine() ?? string.Empty;

        Console.WriteLine("POV Character:");
        chapter.POV = Console.ReadLine() ?? string.Empty;

        Console.WriteLine("Follows Chapter:");
        var cname = Console.ReadLine() ?? string.Empty;
        var previousChapter = chapters.FirstOrDefault(x => x.ChapterName.Equals(cname));
        if (previousChapter == null) return;

        if (previousChapter is MoveableChapter mc)
        {
            chapter.EarlySeason = mc.EarlySeason;
            chapter.EarlyYear = mc.EarlyYear;
            chapter.EarlySortOrder = mc.EarlySortOrder + "a";
        }
        else
        {
            chapter.EarlySeason = previousChapter.Season;
            chapter.EarlyYear = previousChapter.Year;
            chapter.EarlySortOrder = previousChapter.SortOrder + "a";
        }

        Console.WriteLine("Enter Source Files:");
        var a = Console.ReadLine();
        while (!string.IsNullOrWhiteSpace(a))
        {
            chapter.OriginalFilenames.Add(a);
            a = Console.ReadLine();
        }

        Console.WriteLine("Process in Part One?");
        chapter.ProcessedInPartOne = GetYN();
        Console.WriteLine("Process in Part Two?");
        chapter.ProcessedInPartTwo = GetYN();
        Console.WriteLine("Process in Part Three?");
        chapter.ProcessedInPartThree = GetYN();
        Console.WriteLine("Process in Part Four?");
        chapter.ProcessedInPartFour = GetYN();
        Console.WriteLine("Process in Part Five?");
        chapter.ProcessedInPartFive = GetYN();

        book.BonusChapters.Add(chapter);

        await SaveAll();
    }

    private static bool GetYN()
    {
        while (true)
        {
            switch (Console.ReadKey().Key)
            {
                case ConsoleKey.Y:
                    return true;
                case ConsoleKey.N:
                    return false;
            }
        }
    }

    private static async Task CreateTables()
    {
        var chapters = Configuration.Volumes.SelectMany(x =>
        {
            var c = new List<Chapter>();
            c.AddRange(x.POVChapters);
            c.AddRange(x.MangaChapters);
            c.AddRange(x.BonusChapters);
            c.AddRange(x.Chapters);
            return c;
        }).OrderBy(x => (x is MoveableChapter xx) ? xx.EarlySortOrder : x.SortOrder).ToArray();

        //POV Chart
        var sb = new StringBuilder();
        sb.AppendLine("|Character|Chapter|Name|");
        sb.Append("|-|-|-|");
        string character = "";
        foreach (var chapter in chapters.Where(x => x is BonusChapter || x is POVChapter).OrderBy(x => x is BonusChapter c ? c.POV : ((POVChapter)x).POV))
        {
            if (chapter is BonusChapter b)
            {
                if (!string.IsNullOrWhiteSpace(b.POV))
                {
                    if (string.Equals(character, b.POV))
                    {
                        sb.AppendLine($"| |{b.Source}|**{b.ChapterName}**");
                    }
                    else
                    {
                        character = b.POV;
                        sb.AppendLine("");
                        sb.AppendLine($"|{b.POV}|{b.Source}|**{b.ChapterName}**");
                    }
                }
            }
            if (chapter is POVChapter p)
            {
                if (!string.IsNullOrWhiteSpace(p.POV))
                {
                    if (string.Equals(character, p.POV))
                    {
                        sb.AppendLine($"| |{p.GetVolumeName()}|*{p.ChapterName}*");
                    }
                    else
                    {
                        character = p.POV;
                        sb.AppendLine("");
                        sb.AppendLine($"|{p.POV}|{p.GetVolumeName()}|*{p.ChapterName}*");
                    }
                }
            }
        }

        await Task.WhenAll(
            File.WriteAllTextAsync("POVs.txt", sb.ToString()),

            //Chronological Chart P1
            PartChart(chapters, "PartOne.txt", partOne: true),
            //Chronological Chart P2
            PartChart(chapters, "PartTwo.txt", partTwo: true),
            //Chronological Chart P3
            PartChart(chapters, "PartThree.txt", partThree: true),
            //Chronological Chart P4
            PartChart(chapters, "PartFour.txt", partFour: true),
            //Chronological Chart P5
            PartChart(chapters, "PartFive.txt", partFive: true),
            //Chronological Chart Hannelore Y5
            PartChart(chapters, "Hannelore.txt", hannelore: true));
    }

    private static async Task PartChart(Chapter[] chapters, string name, bool partOne = false, bool partTwo = false, bool partThree = false, bool partFour = false, bool partFive = false, bool hannelore = false)
    {
        var sb = new StringBuilder();
        sb.AppendLine("|Chapter|Name|POV|");
        sb.Append("|:-:|-|-|");
        int c = 1;
        string? volume = null;
        string? season = null;
        int year = 0;
        foreach (var chapter in chapters.Where(x => x.ProcessedInPartOne == partOne && x.ProcessedInPartTwo == partTwo && x.ProcessedInPartThree == partThree && x.ProcessedInPartFour == partFour && x.ProcessedInPartFive == partFive && x.ProcessedInHannelore == hannelore))
        {
            if (!string.Equals(volume, chapter.Volume))
            {
                sb.AppendLine();
                volume = chapter.Volume;
                c = 1;
            }


            if (chapter is BonusChapter b)
            {
                if (!string.Equals(season, b.EarlySeason))
                {
                    sb.AppendLine($"|**Year {b.EarlyYear} {b.EarlySeason}**|||");
                    season = b.EarlySeason;
                    year = b.EarlyYear;
                }
                sb.AppendLine($"|{b.Source}|*{b.ChapterName}*|{b.POV}");
            }
            else if (chapter is POVChapter p)
            {
                if (!string.Equals(season, chapter.Season))
                {
                    sb.AppendLine($"|**Year {chapter.Year} {chapter.Season}**|||");
                    season = chapter.Season;
                    year = chapter.Year;
                }
                sb.AppendLine($"|**{p.GetVolumeName()}**|**{p.ChapterName}**|{p.POV}");
            }
            else
            {
                if (!string.Equals(season, chapter.Season))
                {
                    sb.AppendLine($"|**Year {chapter.Year} {chapter.Season}**|||");
                    season = chapter.Season;
                    year = chapter.Year;
                }
                sb.AppendLine($"|{chapter.GetVolumeName()}C{c}|{chapter.ChapterName}");
                c++;
            }
        }

        await File.WriteAllTextAsync(name, sb.ToString());
    }
#endif
}