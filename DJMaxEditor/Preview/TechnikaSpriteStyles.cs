using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace DJMaxEditor.Preview
{
    /// <summary>
    /// One selectable note-sprite source for the TECHNIKA playfield. A style is either a
    /// folder of arcade sheets - one numbered Technika 3 set, or the legacy Technika 2
    /// extraction - or the packaged glyphs compiled into this assembly. The preview used
    /// to resolve a single implicit local root; the style is that choice made explicit and
    /// persisted, so an owner of the arcade data can flip between the thirteen colour sets
    /// of each mode the way the arcade's own option screen does.
    ///
    /// <para><see cref="Id"/> is what survives in the user settings file, so it is stable
    /// and location-independent: <c>"auto"</c>, <c>"packaged"</c>, <c>"t2local"</c> and
    /// <c>"t3:star:3"</c> / <c>"t3:pop:7"</c> style references. The numbered Technika 3 ids
    /// carry no path, which keeps a settings file written against one install valid on
    /// another machine whose T3 lives somewhere else - the catalog re-resolves the path
    /// every time it is built.</para>
    /// </summary>
    internal sealed class TechnikaSpriteStyle
    {
        /// <summary>Pseudo-style that follows the classic probe: legacy local root first,
        /// then the first Technika 3 set, then packaged glyphs.</summary>
        public const string AutoId = "auto";

        /// <summary>The built-in resource table, always available, always last in the catalog.</summary>
        public const string PackagedId = "packaged";

        /// <summary>The legacy single-root source: env var or <c>LocalAssets\Technika2</c>.</summary>
        public const string Technika2LocalId = "t2local";

        internal TechnikaSpriteStyle(string id, string displayName, string noteRoot)
        {
            Id = id;
            DisplayName = displayName;
            NoteRoot = noteRoot;
        }

        /// <summary>The persisted, machine-independent identifier.</summary>
        public string Id { get; private set; }

        /// <summary>What the SPRITE SET dropdown shows.</summary>
        public string DisplayName { get; private set; }

        /// <summary>
        /// The folder the arcade sheets load from, or null for <see cref="PackagedId"/> and
        /// <see cref="AutoId"/> (both resolve their root at load time, not here). Never
        /// stored: rebuilt from the catalog probe on every session.
        /// </summary>
        public string NoteRoot { get; private set; }

        /// <summary>True when the style re-probes the filesystem like the pre-style preview did.</summary>
        public bool IsAuto
        {
            get { return Id == AutoId; }
        }

        /// <summary>True when the only source is the compiled-in resource table.</summary>
        public bool IsPackaged
        {
            get { return Id == PackagedId; }
        }
    }

    /// <summary>
    /// The ordered list of sprite styles the SPRITE SET dropdown offers, built by probing
    /// the filesystem read-only every time it is needed. Ordering is a precedence: AUTO
    /// first, then the legacy Technika 2 source (so an existing setup keeps winning, exactly
    /// as it did before styles existed), then the Technika 3 star sets and pop sets in
    /// arcade numbering order, then every other recognized folder dropped into
    /// <c>&lt;exe&gt;\LocalAssets</c> under any name, and PACKAGED last so there is always
    /// at least one entry that cannot vanish when a drive is unplugged.
    ///
    /// <para>The Technika 3 root resolves from, in order: the <c>Technika3AssetPath</c> user
    /// setting (an owner may keep the data anywhere), the <c>DJMAX_EDITOR_TECHNIKA_ASSETS</c>
    /// environment variable when it happens to point inside a MainGame tree, a Steam-library
    /// sweep over every drive letter for <c>SteamLibrary\steamapps\common\DJMax Technika 3*​</c>,
    /// and finally <c>&lt;exe&gt;\LocalAssets\Technika3</c> for a mirrored portable copy. The
    /// LocalAssets scan behind the local styles needs no name at all: any folder holding a
    /// MainGame tree, a single skin, or a numbered pack is adopted and named after its
    /// folder. All probes are existence checks only; nothing outside the editor's own
    /// folders is ever written, and an unreadable candidate simply loses to the next
    /// one.</para>
    /// </summary>
    internal static class TechnikaSpriteCatalog
    {
        /// <summary>Same variable TechnikaNoteSprites has always read; here it also seeds the
        /// T3 tree walk, so an owner who aimed it at <c>MainGame\note\star\3</c> gets the full
        /// style list for free.</summary>
        private const string PathVariable = "DJMAX_EDITOR_TECHNIKA_ASSETS";

        private const string StarMode = "star";
        private const string PopMode = "pop";

        /// <summary>The marker folder of a Technika 3 Resource\MainGame tree.</summary>
        private const string MainGameResource = "Resource";

        private const string MainGameFolder = "MainGame";

        /// <summary>The one file every usable note set is expected to carry; a numbered folder
        /// without it is data the renderer cannot draw, so it is skipped rather than offered.</summary>
        private const string BasicGlyph = "Note_Basic.png";

        /// <summary>How far to climb from a user-picked (or env-pointed) folder before giving
        /// up on finding the MainGame root above it. Reaches MainGame from MainGame\note\star\3
        /// with room to spare, and stops a mistaken pick from walking a whole drive.</summary>
        private const int MainGameSearchDepth = 8;

        /// <summary>Builds the catalog against the persisted user setting.</summary>
        public static IList<TechnikaSpriteStyle> Build()
        {
            return Build(FeatureFlags.Technika3AssetPath);
        }

        /// <summary>
        /// Builds the catalog. <paramref name="technika3AssetPath"/> overrides the persisted
        /// setting (empty = probe everything else); split out so the catalog can be tested
        /// against a synthetic tree without touching real settings or real drives.
        /// </summary>
        public static IList<TechnikaSpriteStyle> Build(string technika3AssetPath)
        {
            var styles = new List<TechnikaSpriteStyle>();
            styles.Add(new TechnikaSpriteStyle(
                TechnikaSpriteStyle.AutoId, "AUTO", null));

            // The legacy source keeps its old precedence: an owner who already has the env
            // var or the Technika2 folder working must see no behaviour change on upgrade.
            string legacy = TechnikaNoteSprites.ProbeLocalRoot();
            if (legacy != null)
            {
                styles.Add(new TechnikaSpriteStyle(
                    TechnikaSpriteStyle.Technika2LocalId, "TECHNIKA 2 (LOCAL)", legacy));
            }

            string mainGame = ResolveTechnika3Root(technika3AssetPath);
            if (mainGame != null)
            {
                AddNumberedSets(styles, mainGame, StarMode, "t3", "T3");
                AddNumberedSets(styles, mainGame, PopMode, "t3", "T3");
            }

            // Any other folder dropped straight into LocalAssets is a sprite source in its
            // own right - no particular folder name is required. The classic-named folders
            // (Technika2, Technika3) are skipped here because the probes above already
            // claim them with their legacy style ids.
            string editorDirectory = EditorDirectory();
            if (editorDirectory != null)
            {
                string localAssets = Path.Combine(editorDirectory, "LocalAssets");
                if (Directory.Exists(localAssets))
                {
                    styles.AddRange(ProbeLocalAssetFolders(localAssets));
                }
            }

            styles.Add(new TechnikaSpriteStyle(
                TechnikaSpriteStyle.PackagedId, "PACKAGED", null));
            return styles;
        }

        /// <summary>Resolves a persisted id against a freshly built catalog.</summary>
        public static TechnikaSpriteStyle Resolve(string id)
        {
            return Resolve(id, Build());
        }

        /// <summary>
        /// The style an id refers to. <c>"auto"</c>, a null or empty id, and an id whose
        /// directory has since disappeared (a deleted mirror, an uninstalled game, a settings
        /// file carried to a machine without the data) all resolve to the same answer as
        /// AUTO: the first arcade style in the catalog, or PACKAGED when no arcade source
        /// exists at all. Falling back instead of failing is what keeps a stale settings
        /// file from blanking the preview.
        /// </summary>
        public static TechnikaSpriteStyle Resolve(
            string id, IList<TechnikaSpriteStyle> styles)
        {
            if (styles == null || styles.Count == 0)
            {
                styles = Build();
            }

            if (!string.IsNullOrWhiteSpace(id))
            {
                for (int i = 0; i < styles.Count; i++)
                {
                    // AUTO is a question, not an answer: matching it here would hand the
                    // caller the pseudo-style itself, which has no note root to draw from.
                    if (styles[i].IsAuto)
                    {
                        continue;
                    }
                    if (string.Equals(styles[i].Id, id, StringComparison.OrdinalIgnoreCase))
                    {
                        return styles[i];
                    }
                }
            }

            // AUTO's answer is also the fallback for everything unresolvable: the first
            // style that is neither AUTO itself nor the packaged last resort.
            for (int i = 0; i < styles.Count; i++)
            {
                if (!styles[i].IsAuto && !styles[i].IsPackaged)
                {
                    return styles[i];
                }
            }
            return styles[styles.Count - 1];
        }

        /// <summary>
        /// The MainGame root at or above <paramref name="path"/>: the given folder itself
        /// when it already looks like MainGame, otherwise the nearest ancestor that does.
        /// Accepting any depth inside the tree (an owner browsing to MainGame\note\star\3,
        /// say) means the picker never has to hit one exact folder for the choice to work.
        /// </summary>
        internal static string ResolveMainGameRoot(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            try
            {
                if (!Directory.Exists(path))
                {
                    return null;
                }

                string current = Path.GetFullPath(path);
                for (int depth = 0; depth < MainGameSearchDepth && current != null; depth++)
                {
                    if (LooksLikeMainGame(current))
                    {
                        return current;
                    }
                    current = Path.GetDirectoryName(current);
                }
            }
            catch (IOException)
            {
                // An unreadable pick is simply not a MainGame root.
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (ArgumentException)
            {
                // A pick that is not a legal path fragment.
            }
            return null;
        }

        /// <summary>The signature of a Technika 3 MainGame tree: the note folder carrying
        /// at least one of the two numbered modes. Deliberately shallow - the per-set
        /// Note_Basic.png check happens while enumerating, so a half-copied tree still
        /// yields the sets it actually has.</summary>
        private static bool LooksLikeMainGame(string candidate)
        {
            try
            {
                return Directory.Exists(Path.Combine(candidate, "note", StarMode)) ||
                    Directory.Exists(Path.Combine(candidate, "note", PopMode));
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        /// <summary>Where the Technika 3 data lives this session, in precedence order:
        /// explicit setting, environment, installed Steam copies, portable mirror.</summary>
        private static string ResolveTechnika3Root(string technika3AssetPath)
        {
            string root = ResolveMainGameRoot(technika3AssetPath);
            if (root != null)
            {
                return root;
            }

            root = ResolveMainGameRoot(Environment.GetEnvironmentVariable(PathVariable));
            if (root != null)
            {
                return root;
            }

            root = ProbeInstalledTechnika3();
            if (root != null)
            {
                return root;
            }

            string directory = EditorDirectory();
            if (directory != null)
            {
                root = ResolveMainGameRoot(
                    Path.Combine(directory, "LocalAssets", "Technika3"));
            }
            return root;
        }

        /// <summary>
        /// The first usable install on any drive. Only <c>Directory.Exists</c> and one level
        /// of listing per Steam library; a machine without the game pays a handful of failed
        /// existence checks, and a machine with several installs gets the alphabetically
        /// first that has a complete MainGame tree (the user setting disambiguates).
        /// </summary>
        private static string ProbeInstalledTechnika3()
        {
            string[] drives;
            try
            {
                drives = Directory.GetLogicalDrives();
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }

            foreach (string drive in drives)
            {
                string common = Path.Combine(drive, "SteamLibrary", "steamapps", "common");
                string[] installs;
                try
                {
                    if (!Directory.Exists(common))
                    {
                        continue;
                    }
                    installs = Directory.GetDirectories(common, "DJMax Technika 3*");
                }
                catch (IOException)
                {
                    continue;
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }

                Array.Sort(installs, StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < installs.Length; i++)
                {
                    string root = ResolveMainGameRoot(Path.Combine(
                        installs[i], MainGameResource, MainGameFolder));
                    if (root != null)
                    {
                        return root;
                    }
                }
            }
            return null;
        }

        /// <summary>The editor's own folder (or the test process's), for the portable
        /// LocalAssets mirrors.</summary>
        private static string EditorDirectory()
        {
            string directory = Path.GetDirectoryName(
                typeof(TechnikaSpriteCatalog).Assembly.Location);
            if (string.IsNullOrWhiteSpace(directory))
            {
                directory = AppDomain.CurrentDomain.BaseDirectory;
            }
            return string.IsNullOrWhiteSpace(directory) ? null : directory;
        }

        /// <summary>
        /// Every recognized sprite source directly inside a LocalAssets folder, named by
        /// its folder so no particular name is required. A MainGame-shaped tree yields its
        /// numbered star/pop sets; a folder carrying the tap sheet itself is one style; a
        /// folder of numbered set folders (a Technika 2 style pack under an arbitrary name)
        /// yields one style per set. Folders the classic probes already claim (Technika2,
        /// Technika3) are skipped so nothing shows up twice, and unrecognized folders are
        /// ignored. Folders are taken in name order so the dropdown is stable.
        /// </summary>
        internal static IList<TechnikaSpriteStyle> ProbeLocalAssetFolders(
            string localAssetsDirectory)
        {
            var styles = new List<TechnikaSpriteStyle>();
            string[] children;
            try
            {
                if (string.IsNullOrWhiteSpace(localAssetsDirectory) ||
                    !Directory.Exists(localAssetsDirectory))
                {
                    return styles;
                }
                children = Directory.GetDirectories(localAssetsDirectory);
            }
            catch (IOException)
            {
                return styles;
            }
            catch (UnauthorizedAccessException)
            {
                return styles;
            }

            Array.Sort(children, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < children.Length; i++)
            {
                string folder = children[i];
                string name = Path.GetFileName(folder);
                if (IsReservedLocalFolder(name))
                {
                    continue;
                }

                if (LooksLikeMainGame(folder))
                {
                    AddNumberedSets(
                        styles, folder, StarMode, "local:" + name, name.ToUpperInvariant());
                    AddNumberedSets(
                        styles, folder, PopMode, "local:" + name, name.ToUpperInvariant());
                    continue;
                }

                if (File.Exists(Path.Combine(folder, BasicGlyph)))
                {
                    styles.Add(new TechnikaSpriteStyle(
                        "local:" + name,
                        name.ToUpperInvariant(),
                        Path.GetFullPath(folder)));
                    continue;
                }

                AddNumberedPackSets(styles, folder, name);
            }
            return styles;
        }

        /// <summary>Claimed by the classic probes: Technika2 is the legacy source and
        /// Technika3 the portable MainGame mirror, both already catalogued with their own
        /// style ids, so the folder scan must not adopt them a second time.</summary>
        private static bool IsReservedLocalFolder(string name)
        {
            return string.Equals(name, "Technika2", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "Technika3", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// One mode's numbered sets as catalog entries, in arcade numbering order (0..12,
        /// not the string order that puts 10 before 2). Folders whose names are not numbers,
        /// or that lack the tap sheet, are not sets the renderer could draw, so they stay
        /// out of the dropdown rather than appearing as styles that render nothing.
        /// <paramref name="idPrefix"/> namespaces the persisted id ("t3" for the install
        /// tree, "local:&lt;folder&gt;" for a mirrored one); <paramref name="displayPrefix"/>
        /// is what the dropdown shows before the mode and number.
        /// </summary>
        private static void AddNumberedSets(
            List<TechnikaSpriteStyle> styles,
            string mainGame,
            string mode,
            string idPrefix,
            string displayPrefix)
        {
            List<KeyValuePair<int, string>> sets =
                EnumerateNumberedSets(Path.Combine(mainGame, "note", mode));
            string modeName = mode.ToUpperInvariant();
            for (int i = 0; i < sets.Count; i++)
            {
                int number = sets[i].Key;
                styles.Add(new TechnikaSpriteStyle(
                    idPrefix + ":" + mode + ":" + number.ToString(CultureInfo.InvariantCulture),
                    displayPrefix + " " + modeName + " #" +
                        number.ToString("00", CultureInfo.InvariantCulture),
                    Path.GetFullPath(sets[i].Value)));
            }
        }

        /// <summary>A non-MainGame folder whose numbered children are note sets - the
        /// Technika 2 pack shape under an arbitrary name. One style per numbered child, so
        /// the CoolBomb walk keeps matching bursts by set name through the parent folder.</summary>
        private static void AddNumberedPackSets(
            List<TechnikaSpriteStyle> styles, string folder, string name)
        {
            List<KeyValuePair<int, string>> sets = EnumerateNumberedSets(folder);
            string displayPrefix = name.ToUpperInvariant();
            for (int i = 0; i < sets.Count; i++)
            {
                int number = sets[i].Key;
                styles.Add(new TechnikaSpriteStyle(
                    "local:" + name + ":" + number.ToString(CultureInfo.InvariantCulture),
                    displayPrefix + " #" + number.ToString("00", CultureInfo.InvariantCulture),
                    Path.GetFullPath(sets[i].Value)));
            }
        }

        /// <summary>The numeric child folders of <paramref name="parent"/> that carry the
        /// tap sheet, sorted by number. Shared by the MainGame-mode enumeration and the
        /// numbered-pack one; a missing or unreadable parent is simply empty.</summary>
        private static List<KeyValuePair<int, string>> EnumerateNumberedSets(string parent)
        {
            var sets = new List<KeyValuePair<int, string>>();
            string[] children;
            try
            {
                if (!Directory.Exists(parent))
                {
                    return sets;
                }
                children = Directory.GetDirectories(parent);
            }
            catch (IOException)
            {
                return sets;
            }
            catch (UnauthorizedAccessException)
            {
                return sets;
            }

            for (int i = 0; i < children.Length; i++)
            {
                int number;
                if (!int.TryParse(
                        Path.GetFileName(children[i]),
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out number))
                {
                    continue;
                }
                if (!File.Exists(Path.Combine(children[i], BasicGlyph)))
                {
                    continue;
                }
                sets.Add(new KeyValuePair<int, string>(number, children[i]));
            }

            sets.Sort((a, b) => a.Key.CompareTo(b.Key));
            return sets;
        }
    }
}
