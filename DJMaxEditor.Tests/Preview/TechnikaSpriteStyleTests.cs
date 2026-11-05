using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using DJMaxEditor.Preview;

namespace DJMaxEditor.Tests
{
    /// <summary>
    /// Sprite-style catalog and ForStyle loading tests. Every T3 tree here is synthetic -
    /// small PNGs generated into a temp folder at run time and deleted afterwards - so the
    /// tests never touch the real arcade data (or the real drives) and never leave files
    /// behind. The one environment variable the probe reads is cleared around each catalog
    /// build and restored afterwards, so a developer machine with the variable set still
    /// sees the synthetic and only the synthetic.
    /// </summary>
    internal static partial class Program
    {
        private const string AssetsVariable = "DJMAX_EDITOR_TECHNIKA_ASSETS";

        private static void RunTechnikaSpriteStyleTests()
        {
            Test("SpriteCatalog_EnumeratesNumberedSetsInNumericOrder", () =>
            {
                string root = NewTempDir();
                string previous = BlankAssetsVariable();
                try
                {
                    // Deliberately unsorted and polluted: 10 must sort after 2 (arcade
                    // numbering, not string order), 7 lacks the tap sheet and abc is not
                    // a number at all - neither may appear as a style.
                    WritePng(Path.Combine(root, "note", "star", "0", "Note_Basic.png"), 4, 4);
                    WritePng(Path.Combine(root, "note", "star", "10", "Note_Basic.png"), 4, 4);
                    WritePng(Path.Combine(root, "note", "star", "2", "Note_Basic.png"), 4, 4);
                    WritePng(Path.Combine(root, "note", "star", "7", "notegage.png"), 4, 4);
                    Directory.CreateDirectory(Path.Combine(root, "note", "star", "abc"));
                    WritePng(Path.Combine(root, "note", "pop", "3", "Note_Basic.png"), 4, 4);

                    IList<TechnikaSpriteStyle> styles = TechnikaSpriteCatalog.Build(root);
                    AssertStyleIds(styles,
                        "auto",
                        "t3:star:0", "t3:star:2", "t3:star:10",
                        "t3:pop:3",
                        "packaged");

                    TechnikaSpriteStyle star10 = FindStyle(styles, "t3:star:10");
                    AssertTrue(star10.DisplayName == "T3 STAR #10",
                        "display name should use zero-padded arcade numbering");
                    AssertTrue(star10.NoteRoot == Path.GetFullPath(
                            Path.Combine(root, "note", "star", "10")),
                        "style note root should be the absolute set folder");
                    AssertTrue(FindStyle(styles, "t3:pop:3").DisplayName == "T3 POP #03",
                        "pop display name should use zero-padded arcade numbering");
                }
                finally
                {
                    RestoreAssetsVariable(previous);
                    Directory.Delete(root, true);
                }
            });

            Test("SpriteCatalog_LegacyLocalRootKeepsFirstArcadePrecedence", () =>
            {
                string root = NewTempDir();
                string legacy = NewTempDir();
                string previous = BlankAssetsVariable();
                try
                {
                    WritePng(Path.Combine(root, "note", "star", "0", "Note_Basic.png"), 4, 4);
                    WritePng(Path.Combine(legacy, "Note_Basic.png"), 4, 4);
                    Environment.SetEnvironmentVariable(AssetsVariable, legacy);

                    IList<TechnikaSpriteStyle> styles = TechnikaSpriteCatalog.Build(root);
                    AssertStyleIds(styles, "auto", "t2local", "t3:star:0", "packaged");

                    // The upgrade contract: a working pre-style setup must keep winning,
                    // and AUTO must answer with it exactly as the old probe did.
                    TechnikaSpriteStyle local = FindStyle(styles, "t2local");
                    AssertTrue(local.NoteRoot == Path.GetFullPath(legacy),
                        "legacy style should point at the probed legacy root");
                    AssertTrue(object.ReferenceEquals(
                            TechnikaSpriteCatalog.Resolve("auto", styles), local),
                        "AUTO must resolve to the legacy source ahead of any T3 set");
                }
                finally
                {
                    RestoreAssetsVariable(previous);
                    Directory.Delete(legacy, true);
                    Directory.Delete(root, true);
                }
            });

            Test("SpriteCatalog_EnvVarInsideMainGameTreeResolves", () =>
            {
                string root = NewTempDir();
                string previous = BlankAssetsVariable();
                try
                {
                    WritePng(Path.Combine(root, "note", "star", "0", "Note_Basic.png"), 4, 4);

                    // An owner who aimed the variable at one set inside the tree (the
                    // pre-style usage) gets the whole style list: the root is found by
                    // climbing, and a bogus explicit setting must fall through to it.
                    // The legacy probe also claims the env-var folder as t2local - that
                    // duplication is the compatibility contract: a working pre-style
                    // pointer keeps drawing exactly what it drew before styles existed
                    // (here that happens to be the same art as T3 STAR #00).
                    Environment.SetEnvironmentVariable(
                        AssetsVariable, Path.Combine(root, "note", "star", "0"));
                    foreach (string setting in new[] { "", Path.Combine(root, "missing") })
                    {
                        IList<TechnikaSpriteStyle> styles = TechnikaSpriteCatalog.Build(setting);
                        AssertStyleIds(styles, "auto", "t2local", "t3:star:0", "packaged");
                        AssertTrue(FindStyle(styles, "t3:star:0").NoteRoot ==
                                Path.GetFullPath(Path.Combine(root, "note", "star", "0")),
                            $"setting '{setting}' should fall through to the env-var tree");
                        AssertTrue(FindStyle(styles, "t2local").NoteRoot ==
                                Path.GetFullPath(Path.Combine(root, "note", "star", "0")),
                            "the legacy mechanism should keep claiming the env-var folder");
                    }
                }
                finally
                {
                    RestoreAssetsVariable(previous);
                    Directory.Delete(root, true);
                }
            });

            Test("SpriteCatalog_MainGameRootFoundFromAnyDepth", () =>
            {
                string root = NewTempDir();
                string outside = NewTempDir();
                try
                {
                    WritePng(Path.Combine(root, "note", "pop", "1", "Note_Basic.png"), 4, 4);
                    string deep = Path.Combine(root, "note", "pop", "1");

                    AssertTrue(TechnikaSpriteCatalog.ResolveMainGameRoot(deep) ==
                            Path.GetFullPath(root),
                        "the MainGame root should be found from a set folder deep inside it");
                    AssertTrue(TechnikaSpriteCatalog.ResolveMainGameRoot(root) ==
                            Path.GetFullPath(root),
                        "the MainGame root should resolve to itself");
                    AssertTrue(TechnikaSpriteCatalog.ResolveMainGameRoot(outside) == null,
                        "a folder with no MainGame signature above it must not resolve");
                    AssertTrue(TechnikaSpriteCatalog.ResolveMainGameRoot(
                            Path.Combine(root, "missing")) == null,
                        "a folder that does not exist must not resolve");
                }
                finally
                {
                    Directory.Delete(outside, true);
                    Directory.Delete(root, true);
                }
            });

            Test("SpriteCatalog_UnresolvableIdsFallBackLikeAuto", () =>
            {
                // Hand-built catalogs keep this independent of what any test machine
                // happens to have installed: the fallback rule is about the list's shape,
                // not about probing.
                var arcade = new TechnikaSpriteStyle(
                    "t3:star:3", "T3 STAR #03", @"C:\fake\note\star\3");
                var packagedOnly = new List<TechnikaSpriteStyle>
                {
                    new TechnikaSpriteStyle("auto", "AUTO", null),
                    new TechnikaSpriteStyle("packaged", "PACKAGED", null),
                };
                var withArcade = new List<TechnikaSpriteStyle>
                {
                    new TechnikaSpriteStyle("auto", "AUTO", null),
                    arcade,
                    new TechnikaSpriteStyle("packaged", "PACKAGED", null),
                };

                foreach (string id in new[] { null, "", "auto", "t3:star:99" })
                {
                    AssertTrue(object.ReferenceEquals(
                            TechnikaSpriteCatalog.Resolve(id, withArcade), arcade),
                        $"id '{id}' should fall back to the first arcade style");
                }
                AssertTrue(object.ReferenceEquals(
                        TechnikaSpriteCatalog.Resolve("t3:star:3", withArcade), arcade),
                    "a live id must resolve to its own style");
                AssertTrue(withArcade[withArcade.Count - 1].IsPackaged,
                    "packaged must stay the last catalog entry");

                foreach (string id in new[] { null, "auto", "t3:star:3", "packaged" })
                {
                    TechnikaSpriteStyle resolved = TechnikaSpriteCatalog.Resolve(id, packagedOnly);
                    AssertTrue(resolved.IsPackaged,
                        $"id '{id}' with no arcade source must resolve to packaged");
                }
            });

            Test("SpriteCatalog_LocalAssetsFoldersNeedNoSpecialName", () =>
            {
                // Stands in for <exe>\LocalAssets: every shape of sprite folder under an
                // arbitrary name must be adopted, reserved classic names must not double up.
                string localAssets = NewTempDir();
                try
                {
                    // A single skin folder under its own name.
                    WritePng(Path.Combine(localAssets, "AlphaSkin", "Note_Basic.png"), 4, 4);
                    // A Technika 2 style numbered pack under an arbitrary name.
                    WritePng(Path.Combine(localAssets, "MyPack", "10", "Note_Basic.png"), 4, 4);
                    WritePng(Path.Combine(localAssets, "MyPack", "2", "Note_Basic.png"), 4, 4);
                    WritePng(Path.Combine(localAssets, "MyPack", "x", "Note_Basic.png"), 4, 4);
                    // A MainGame mirror under an arbitrary name.
                    WritePng(Path.Combine(
                        localAssets, "Mirror", "note", "star", "1", "Note_Basic.png"), 4, 4);
                    WritePng(Path.Combine(
                        localAssets, "Mirror", "note", "pop", "0", "Note_Basic.png"), 4, 4);
                    // Reserved: the classic probes claim these names with their own ids.
                    WritePng(Path.Combine(localAssets, "Technika2", "Note_Basic.png"), 4, 4);
                    WritePng(Path.Combine(
                        localAssets, "Technika3", "note", "star", "0", "Note_Basic.png"), 4, 4);
                    // Not a sprite folder at all.
                    Directory.CreateDirectory(Path.Combine(localAssets, "Junk"));

                    IList<TechnikaSpriteStyle> styles =
                        TechnikaSpriteCatalog.ProbeLocalAssetFolders(localAssets);

                    AssertStyleIds(styles,
                        "local:AlphaSkin",
                        "local:Mirror:star:1", "local:Mirror:pop:0",
                        "local:MyPack:2", "local:MyPack:10");

                    TechnikaSpriteStyle flat = FindStyle(styles, "local:AlphaSkin");
                    AssertTrue(flat.DisplayName == "ALPHASKIN",
                        "a flat skin should be named after its folder");
                    AssertTrue(flat.NoteRoot == Path.GetFullPath(
                            Path.Combine(localAssets, "AlphaSkin")),
                        "a flat skin's root is the folder itself");

                    TechnikaSpriteStyle mirror = FindStyle(styles, "local:Mirror:star:1");
                    AssertTrue(mirror.DisplayName == "MIRROR STAR #01",
                        "a mirrored MainGame set should keep mode naming");
                    AssertTrue(mirror.NoteRoot == Path.GetFullPath(
                            Path.Combine(localAssets, "Mirror", "note", "star", "1")),
                        "a mirrored MainGame set root should be the set folder");

                    TechnikaSpriteStyle pack = FindStyle(styles, "local:MyPack:10");
                    AssertTrue(pack.DisplayName == "MYPACK #10",
                        "numbered pack sets should sort and pad numerically");

                    // The adopted styles load like any other: the pack's set 2 resolves
                    // its tap sheet from the numbered folder.
                    TechnikaNoteSprites sprites = TechnikaNoteSprites.ForStyle(
                        FindStyle(styles, "local:MyPack:2"));
                    AssertTrue(sprites.For(GameplayPreviewNoteKind.Basic) != null,
                        "an adopted pack set should load its tap sheet");
                }
                finally
                {
                    Directory.Delete(localAssets, true);
                }
            });

            Test("SpriteStyle_ForStyleLoadsAndSlicesTheChosenSet", () =>
            {
                string root = NewTempDir();
                string set = Path.Combine(root, "note", "star", "2");
                string previous = BlankAssetsVariable();
                try
                {
                    // Ten square frames in one strip, one hold cap strip of five, a square
                    // line sheet, a ring strip and a two-frame CoolBomb under the matching
                    // numbered folder. CoolBomb\9 is a decoy: the burst must come from the
                    // set the glyphs came from, not from whichever folder resolves first.
                    WritePng(Path.Combine(set, "Note_Basic.png"), 100, 10);
                    WritePng(Path.Combine(set, "line_nor_end.png"), 50, 10);
                    WritePng(Path.Combine(set, "notepressline.png"), 10, 10);
                    WritePng(Path.Combine(set, "note_circle.png"), 20, 10);
                    WritePng(Path.Combine(root, "CoolBomb", "2", "cool", "cool_0000.png"), 8, 6);
                    WritePng(Path.Combine(root, "CoolBomb", "2", "cool", "cool_0001.png"), 8, 6);
                    WritePng(Path.Combine(root, "CoolBomb", "9", "cool", "cool_0000.png"), 30, 30);

                    var style = new TechnikaSpriteStyle("t3:star:2", "T3 STAR #02", set);
                    TechnikaNoteSprites sprites = TechnikaNoteSprites.ForStyle(style);

                    AssertTrue(sprites.UsesLocalAssets, "style sprites must use the local set");
                    AssertTrue(sprites.SourceLabel == "T3 STAR #02",
                        "the style name should be the reported source label");
                    AssertTrue(sprites.LocalRoot == set, "the style root should be used verbatim");

                    TechnikaNoteSprite basic = sprites.For(GameplayPreviewNoteKind.Basic);
                    AssertTrue(basic != null && basic.FrameCount == 10 && basic.FrameSize == 10,
                        "Note_Basic should slice into its ten square frames");
                    AssertTrue(basic.FrameWidth == 10,
                        "a square frame's width should equal its size");

                    // Every arcade strip is a ten-frame loop (see TechnikaNoteSprite):
                    // 50x10 slices into ten 5x10 cap frames, not five.
                    TechnikaNoteSprite cap = sprites.TrailCap(GameplayPreviewNoteKind.Hold, false);
                    AssertTrue(cap != null && cap.FrameCount == 10 && cap.FrameSize == 10,
                        "line_nor_end should slice into its ten cap frames");
                    AssertTrue(cap.FrameWidth == 5,
                        "a cap frame is narrower than it is tall");

                    TechnikaNoteSprite line = sprites.Line(GameplayPreviewNoteKind.ChainHead);
                    AssertTrue(line != null && line.FrameCount == 1,
                        "a square line sheet is one still image, not a strip");

                    AssertTrue(sprites.Ring != null,
                        "note_circle should resolve from the chosen set");
                    AssertTrue(sprites.ReferenceFrameSize == 10,
                        "the tap's frame should be the reference size");

                    TechnikaNoteSprite bomb = sprites.CoolBomb;
                    AssertTrue(bomb != null && bomb.FrameCount == 2,
                        "the CoolBomb sequence should load from the tree beside the notes");
                    AssertTrue(bomb.Frame(0).Width == 8,
                        "the burst must come from CoolBomb\\2\\cool, the set's own folder, " +
                        "not from the CoolBomb\\9 decoy");

                    // An explicit style pins its root: chart rebinds re-probe nothing.
                    AssertTrue(!sprites.RefreshLocalRoot(),
                        "a style-pinned sprite root must not refresh");
                }
                finally
                {
                    RestoreAssetsVariable(previous);
                    Directory.Delete(root, true);
                }
            });

            Test("SpriteStyle_PackagedStyleIsNullSafeEverywhere", () =>
            {
                var packaged = new TechnikaSpriteStyle("packaged", "PACKAGED", null);
                TechnikaNoteSprites sprites = TechnikaNoteSprites.ForStyle(packaged);

                AssertTrue(!sprites.UsesLocalAssets,
                    "packaged sprites must not look like local assets");
                AssertTrue(sprites.SourceLabel == "PACKAGED GLYPHS",
                    "packaged label mismatch");
                AssertTrue(sprites.Ring == null,
                    "packaged set has no approach ring and must say so");
                AssertTrue(sprites.CoolBomb == null,
                    "packaged set has no CoolBomb tree and must say so");
                AssertTrue(sprites.For(GameplayPreviewNoteKind.Basic) != null,
                    "packaged glyphs must still resolve from the resource table");
                AssertTrue(!sprites.RefreshLocalRoot(),
                    "a packaged root is pinned and must not refresh");

                var renderer = new TechnikaPlayfieldRenderer();
                AssertTrue(renderer.SetSpriteStyle(packaged),
                    "switching to the packaged style should report a change");
                AssertTrue(renderer.SpriteSourceLabel == "PACKAGED GLYPHS",
                    "renderer source label should follow the packaged style");
                AssertTrue(!renderer.SetSpriteStyle(packaged),
                    "re-applying the same style must be a no-op");
            });
        }

        // ---------- sprite-style fixtures ----------

        private static string NewTempDir()
        {
            string path = Path.Combine(
                Path.GetTempPath(), "djme_sprites_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static void WritePng(string path, int width, int height)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var bitmap = new Bitmap(width, height))
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.White);
                bitmap.Save(path, ImageFormat.Png);
            }
        }

        private static string BlankAssetsVariable()
        {
            string previous = Environment.GetEnvironmentVariable(AssetsVariable);
            Environment.SetEnvironmentVariable(AssetsVariable, null);
            return previous;
        }

        private static void RestoreAssetsVariable(string previous)
        {
            Environment.SetEnvironmentVariable(AssetsVariable, previous);
        }

        private static void AssertStyleIds(IList<TechnikaSpriteStyle> styles, params string[] expected)
        {
            AssertTrue(styles.Count == expected.Length,
                "expected [" + string.Join(", ", expected) + "], got [" +
                StyleIdList(styles) + "]");
            for (int i = 0; i < expected.Length; i++)
            {
                AssertTrue(string.Equals(styles[i].Id, expected[i], StringComparison.Ordinal),
                    $"style {i}: expected '{expected[i]}', got '{styles[i].Id}' " +
                    "(full list: " + StyleIdList(styles) + ")");
            }
        }

        private static string StyleIdList(IList<TechnikaSpriteStyle> styles)
        {
            var ids = new List<string>(styles.Count);
            foreach (TechnikaSpriteStyle style in styles)
            {
                ids.Add(style.Id);
            }
            return string.Join(", ", ids);
        }

        private static TechnikaSpriteStyle FindStyle(IList<TechnikaSpriteStyle> styles, string id)
        {
            foreach (TechnikaSpriteStyle style in styles)
            {
                if (string.Equals(style.Id, id, StringComparison.Ordinal))
                {
                    return style;
                }
            }
            throw new Exception("style '" + id + "' missing from catalog");
        }
    }
}
