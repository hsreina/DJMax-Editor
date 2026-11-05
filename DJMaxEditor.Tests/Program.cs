using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using DJMaxEditor;
using DJMaxEditor.DJMax;
using DJMaxEditor.Files;
using DJMaxEditor.Files.FormatDetection;
using DJMaxEditor.Files.bytes;
using DJMaxEditor.Files.pt;
using DJMaxEditor.Files.Tech;

namespace DJMaxEditor.Tests
{
    /// <summary>
    /// Minimal dependency-free test runner. Each test is an Action that throws on failure.
    /// Prints [PASS]/[FAIL]/[SKIP] per test and exits with the number of failures.
    /// Real (copyrighted) sample fixtures are NOT committed; pass "--fixtures &lt;dir&gt;" pointing at the
    /// local DM folder to enable the real-file tests. Without it, those tests report SKIP and the
    /// synthetic in-memory tests still run.
    /// </summary>
    internal static partial class Program
    {
        private static int _pass, _fail, _skip;

        [STAThread]
        private static int Main(string[] args)
        {
            string fixtures = null;
            string timelineSnapshots = null;
            string bmsFixtures = null;
            string perfFile = null;
            string genChartPath = null;
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--fixtures") fixtures = args[i + 1];
                if (args[i] == "--timeline-snapshots") timelineSnapshots = args[i + 1];
                if (args[i] == "--bms-fixtures") bmsFixtures = args[i + 1];
                if (args[i] == "--perf") perfFile = args[i + 1];
                if (args[i] == "--gen-chart") genChartPath = args[i + 1];
            }

            Console.WriteLine("== DJMax Editor test harness ==");
            Console.WriteLine(fixtures != null ? $"fixtures dir: {fixtures}" : "fixtures dir: (none — real-file tests will SKIP)");
            Console.WriteLine();

            // ---- Detector: synthetic in-memory fixtures (no external data) ----
            Test("Detect_Empty_Malformed", () =>
                AssertFormat(ChartFormatDetector.Detect(new byte[0]), ChartFormat.Malformed));

            Test("Detect_TooShort_Malformed", () =>
                AssertFormat(ChartFormatDetector.Detect(new byte[] { 1, 2 }), ChartFormat.Malformed));

            Test("Detect_Garbage_Unknown", () =>
                AssertFormat(ChartFormatDetector.Detect(Garbage()), ChartFormat.Unknown));

            Test("Detect_Xml_ByContent", () =>
                AssertFormat(ChartFormatDetector.Detect(Xml()), ChartFormat.CyclonXml));

            Test("Detect_PtffDecrypted_WithEztr", () =>
            {
                var r = ChartFormatDetector.Detect(SyntheticDecryptedPtff());
                AssertFormat(r, ChartFormat.PtffDecrypted);
                AssertTrue(!r.IsEncrypted, "should not be flagged encrypted");
                AssertTrue(!r.IsReadOnly, "decrypted PTFF is not read-only");
            });

            Test("Detect_PtffEncrypted_NoEztr_BigFlag", () =>
            {
                var r = ChartFormatDetector.Detect(SyntheticEncryptedPtff());
                AssertFormat(r, ChartFormat.PtffEncryptedTechnika);
                AssertTrue(r.IsEncrypted, "must be positively flagged encrypted");
            });

            Test("Detect_Trailer_Valid_Editable", () =>
            {
                var r = ChartFormatDetector.Detect(SyntheticTrailer());
                AssertFormat(r, ChartFormat.TrailerRespectV);
                AssertTrue(!r.IsReadOnly, "Respect trailer should open editable");
                AssertTrue(!r.IsEncrypted, "trailer format is plaintext");
            });

            Test("Detect_Trailer_BadOffset_Malformed", () =>
                AssertFormat(ChartFormatDetector.Detect(SyntheticTrailerBadOffset()), ChartFormat.Malformed));

            Test("Detect_Trailer_Truncated_Malformed", () =>
                AssertFormat(ChartFormatDetector.Detect(SyntheticTrailerTruncated()), ChartFormat.Malformed));

            // Extension must never override content.
            Test("Detect_PtExtension_DoesNotForcePtff", () =>
            {
                // A trailer-format file that happens to be named .pt must be detected as trailer, not PTFF.
                var r = ChartFormatDetector.Detect(SyntheticTrailer(), ".pt");
                AssertFormat(r, ChartFormat.TrailerRespectV);
            });

            Test("Detect_BytesExtension_StillValidated", () =>
            {
                // A .bytes-named garbage file must not be blindly treated as a trailer chart.
                var r = ChartFormatDetector.Detect(Garbage(), ".bytes");
                AssertTrue(r.Format == ChartFormat.Unknown || r.Format == ChartFormat.Malformed,
                    $"expected Unknown/Malformed, got {r.Format}");
            });

            Test("Detect_IsPure_NoThrowOnAnyInput", () =>
            {
                // Detector must never throw, even on hostile inputs (it performs no network/IO/decrypt).
                foreach (var b in HostileInputs())
                {
                    var r = ChartFormatDetector.Detect(b);
                    AssertTrue(r != null, "result must not be null");
                }
            });

            RunBmsFormatTests(fixtures, bmsFixtures);
            RunTechFormatTests();
            RunTrailerSaveRoundTripTests();
            RunSharedEditingTests();

            // ---- Detector: real fixtures (SKIP if not provided) ----
            RealFileTest("Detect_Real_TutorialDecrypted_Ptff", fixtures,
                "TECHNIKA 2 Patterns/tutorial/tutorial_pop_1.pt", data =>
                {
                    var r = ChartFormatDetector.Detect(data, ".pt");
                    AssertFormat(r, ChartFormat.PtffDecrypted);
                    AssertTrue(!r.IsEncrypted, "tutorial is decrypted");
                });

            RealFileTest("Detect_Real_Baramege_EncryptedTechnika", fixtures,
                "TECHNIKA 2 Patterns/@baramege/@baramege_star_1.pt", data =>
                {
                    var r = ChartFormatDetector.Detect(data, ".pt");
                    AssertFormat(r, ChartFormat.PtffEncryptedTechnika);
                    AssertTrue(r.IsEncrypted, "baramege body is encrypted");
                });

            RealFileTest("Detect_Real_RespectV_Wakeup_Trailer", fixtures,
                "_analysis_scratch/charts/wakeup/wakeup_4b_nm.pt", data =>
                {
                    var r = ChartFormatDetector.Detect(data, ".pt");
                    AssertFormat(r, ChartFormat.TrailerRespectV);
                    AssertTrue(!r.IsReadOnly, "Respect V should open editable");
                });

            // ---- Trailer parser (bounds-checked, typed errors, editable in memory) ----
            Test("Trailer_Parse_Synthetic_Clean_Editable", () =>
            {
                var data = BuildTrailer(new byte[] { 1, 2, 3, 4 });
                TrailerReadResult stats;
                var pd = TrailerChartReader.Read(data, false, out stats);
                AssertTrue(!pd.IsReadOnly, "parsed Respect chart should be editable");
                AssertTrue(pd.SourceFormat == ChartFormat.TrailerRespectV, "source format must be trailer");
                AssertTrue(stats.InstrumentsRead == 1, $"expected 1 instrument, got {stats.InstrumentsRead}");
                AssertTrue(stats.TracksRead == 1, $"expected 1 track, got {stats.TracksRead}");
                AssertTrue(stats.EventsRead == 4, $"expected 4 events, got {stats.EventsRead}");
            });

            Test("Trailer_RenameOption_Off_NoRemap", () =>
            {
                var data = BuildTrailer(new byte[] { 1 });
                TrailerReadResult stats;
                TrailerChartReader.Read(data, false, out stats);
                AssertTrue(stats.AudioRemappedWavToOgg == 0, "with rename OFF nothing is remapped");
            });

            Test("Trailer_RenameOption_On_ReportsCount", () =>
            {
                var data = BuildTrailer(new byte[] { 1 }); // instrument name is "1_dr_001.wav"
                TrailerReadResult stats;
                var pd = TrailerChartReader.Read(data, true, out stats);
                AssertTrue(stats.AudioRemappedWavToOgg == 1, $"expected 1 remap, got {stats.AudioRemappedWavToOgg}");
                var remapped = pd.Instruments.Any(i => i.Name != null && i.Name.EndsWith(".ogg"));
                AssertTrue(remapped, "instrument .wav should have become .ogg");
            });

            Test("Trailer_UnsupportedOpcode_Typed", () =>
            {
                var data = BuildTrailer(new byte[] { 1, 9 }); // 9 is not a valid event code
                AssertThrows(ChartLoadError.UnsupportedOpcode, () =>
                {
                    TrailerReadResult s; TrailerChartReader.Read(data, false, out s);
                });
            });

            Test("Trailer_InvalidInstrumentCount_Typed", () =>
            {
                var data = BuildTrailer(new byte[] { 1 });
                PatchU16(data, TrailerOffset(data), 5000); // trailer insCnt = 5000, file far too short
                AssertThrowsAny(() => { TrailerReadResult s; TrailerChartReader.Read(data, false, out s); });
            });

            Test("Trailer_TruncatedTrack_Typed", () =>
            {
                var data = BuildTrailer(new byte[] { 1, 2, 3, 4 });
                PatchU32(data, EventsCountOffset, 100000); // absurd event count
                AssertThrows(ChartLoadError.TruncatedTrack, () =>
                {
                    TrailerReadResult s; TrailerChartReader.Read(data, false, out s);
                });
            });

            Test("Trailer_LongUnterminatedName_NoOverrun", () =>
            {
                // instrument name field fully filled (no null terminator) must not overrun the reader.
                var data = BuildTrailer(new byte[] { 1 }, fillInstrumentName: true);
                TrailerReadResult s;
                var pd = TrailerChartReader.Read(data, false, out s);
                AssertTrue(pd.Instruments.Count >= 2, "should still parse instruments");
            });

            // ---- No silent network / no auto-decrypt ----
            Test("Online_DisabledByDefault", () =>
                AssertTrue(PTFile.OnlineEnabled == false, "online path must default to OFF"));

            RealFileTest("NoNetwork_EncryptedFile_NoOnlineAttempt", fixtures,
                "TECHNIKA 2 Patterns/@baramege/@baramege_star_1.pt", data =>
                {
                    // Save to a temp file and open via the real PTFF loader; it must fail WITHOUT any
                    // online attempt (generic parse failure never triggers decryption/network).
                    PTFile.OnlineAttemptCount = 0;
                    var tmp = Path.Combine(Path.GetTempPath(), "djme_enc_test.pt");
                    File.WriteAllBytes(tmp, data);
                    try
                    {
                        var loader = new PTOpenFile();
                        PlayerData pd;
                        bool ok = loader.Open(tmp, out pd);
                        AssertTrue(!ok, "encrypted body must not parse as decrypted PTFF");
                        AssertTrue(PTFile.OnlineAttemptCount == 0, "no online attempt may be made");
                    }
                    finally { try { File.Delete(tmp); } catch { } }
                });

            // ---- Regression: decrypted PTFF still loads ----
            RealFileTest("Regression_DecryptedPtff_StillLoads", fixtures,
                "TECHNIKA 2 Patterns/tutorial/tutorial_pop_1.pt", data =>
                {
                    var tmp = Path.Combine(Path.GetTempPath(), "djme_dec_test.pt");
                    File.WriteAllBytes(tmp, data);
                    try
                    {
                        var loader = new PTOpenFile();
                        PlayerData pd;
                        bool ok = loader.Open(tmp, out pd);
                        AssertTrue(ok, "decrypted PTFF must still load");
                        AssertTrue(pd.Tracks.Count() > 0, "should have tracks");
                        AssertTrue(!pd.IsReadOnly, "PTFF is editable (not read-only)");
                    }
                    finally { try { File.Delete(tmp); } catch { } }
                });

            // ---- Offline PT codec (B1): in-process decrypt of encrypted Technika/Trilogy charts ----

            // Self-contained: encode<->decode is an exact inverse and the codec is deterministic
            // (no static state leakage between conversions). Proves consistency, not correctness —
            // correctness is proven against the pt.exe oracle below.
            Test("PtCodec_RoundTrip_And_Deterministic_Synthetic", () =>
            {
                var original = SyntheticDecryptedPt(); // 24-byte header + body, first body word <= 10
                var enc = PtCodec.Convert(original);
                AssertTrue(enc.Mode == PtCodec.ModeEncode, "small first body word => encode");
                AssertTrue(!Equal(enc.Data, original), "encryption must change the body");

                var enc2 = PtCodec.Convert(original);
                AssertTrue(Equal(enc.Data, enc2.Data), "codec must be deterministic");

                var dec = PtCodec.Convert(enc.Data);
                AssertTrue(dec.Mode == PtCodec.ModeDecode, "encrypted first body word => decode");
                AssertTrue(Equal(dec.Data, original), "decode(encode(x)) must equal x");

                AssertTrue(Equal(original, SyntheticDecryptedPt()), "input must not be mutated");
            });

            Test("PtCodec_TooShort_Throws", () =>
                AssertThrowsAnyException(() => PtCodec.Convert(new byte[] { 0x50, 0x54, 0x46, 0x46 })));

            // Definitive correctness: my managed decrypt must match @wchdsk's pt.exe byte-for-byte.
            // Oracle file was produced by running pt.exe on the encrypted fixture (see report).
            RealFileTestTwo("PtCodec_Decrypt_MatchesOracle_Baramege", fixtures,
                "TECHNIKA 2 Patterns/@baramege/@baramege_star_1.pt",
                "_analysis_scratch/oracle/decrypted/enc_sample.pt",
                (enc, oracle) =>
                {
                    var mine = PtCodec.Decrypt(enc);
                    AssertTrue(mine.Length == oracle.Length, $"length {mine.Length} != oracle {oracle.Length}");
                    AssertTrue(Equal(mine, oracle), "decrypted bytes must match pt.exe oracle exactly");
                    AssertTrue(Sha256(mine) == Sha256(oracle), "SHA-256 must match oracle");
                });

            // Breadth: match the pt.exe oracle across many real encrypted charts with different headers
            // (=> different MT seeds) and lengths (=> different 8-byte-block tail handling).
            BatchOracleTest("PtCodec_Decrypt_MatchesOracle_Batch", fixtures);

            // The decrypted output is a genuine, parseable PTFF chart (PTFF+EZTR), and opens through
            // the real loader via SourceOverride yielding tracks/instruments — the full B1 open path.
            RealFileTest("PtCodec_Decrypt_ProducesEditablePtff_Baramege", fixtures,
                "TECHNIKA 2 Patterns/@baramege/@baramege_star_1.pt", data =>
                {
                    var decrypted = PtCodec.Decrypt(data);
                    var r = ChartFormatDetector.Detect(decrypted, ".pt");
                    AssertFormat(r, ChartFormat.PtffDecrypted);

                    var loader = new PTOpenFile { SourceOverride = decrypted, FromEncryptedSource = true };
                    PlayerData pd;
                    bool ok = loader.Open("dummy_path.pt", out pd);
                    AssertTrue(ok, "decrypted chart must open through the PTFF loader");
                    AssertTrue(pd.Tracks.Count() > 0, "decrypted chart should have tracks");
                    AssertTrue(pd.Instruments.Count >= 2, "decrypted chart should have instruments");
                    AssertTrue(pd.Encrypted, "must record that the on-disk source was encrypted");
                    AssertTrue(!pd.IsReadOnly, "decrypted PTFF is editable");
                });

            // Real-data round trip: re-encrypting the decrypted chart reproduces the original file.
            RealFileTest("PtCodec_ReEncrypt_ReproducesOriginal_Baramege", fixtures,
                "TECHNIKA 2 Patterns/@baramege/@baramege_star_1.pt", data =>
                {
                    var decrypted = PtCodec.Decrypt(data);
                    var reEncrypted = PtCodec.Encrypt(decrypted);
                    AssertTrue(Equal(reEncrypted, data), "encrypt(decrypt(E)) must reproduce E");
                });

            // ---- Source file is never modified by opening ----
            RealFileTest("SourceFile_Unchanged_AfterOpen", fixtures,
                "_analysis_scratch/charts/wakeup/wakeup_4b_nm.pt", data =>
                {
                    string before = Sha256(data);
                    TrailerReadResult s;
                    TrailerChartReader.Read((byte[])data.Clone(), true, out s); // parse a copy, rename ON
                    string after = Sha256(data);
                    AssertTrue(before == after, "input buffer must be unchanged by reading");
                    AssertTrue(s.TracksRead > 0 && s.EventsRead > 0, "real chart should yield tracks/events");
                });

            RunTimelineFoundationTests();
            RunTimelineCoordinateTests();
            RunTimelineEventIndexTests();
            RunTimelineProjectionTests();
            RunTimelineRulerTests();
            RunTimelineRenderingTests();
            RunTimelineSurfaceTests(fixtures);
            RunTimelineV2ParityTests(fixtures);
            RunGameplayPreviewTests();
            RunTechnikaSpriteStyleTests();
            RunStudioThemeTests();
            RunStudioShellFoundationTests();
            RunSoundNoteSearchTests();
            if (!string.IsNullOrEmpty(timelineSnapshots))
            {
                ExportTimelineSnapshots(fixtures, timelineSnapshots);
            }
            if (!string.IsNullOrEmpty(perfFile))
            {
                RunEditorRenderPerfTests(perfFile);
                RunEditorHostedPerfTests(perfFile);
            }
            if (!string.IsNullOrEmpty(genChartPath))
            {
                // Perf-harness feeder: a dense synthetic .tech through the real writer
                // so --perf exercises the real parse path without committed fixtures.
                var model = Fixtures.SyntheticChartFactory.Create(24, 3000, 24);
                File.WriteAllText(genChartPath, TechmaniaChartSerializer.Serialize(model));
                Console.WriteLine($"[GEN] wrote {genChartPath} " +
                    $"({model.Tracks.Count()} tracks x 3000 events)");
            }

            Console.WriteLine();
            Console.WriteLine($"== RESULT: {_pass} passed, {_fail} failed, {_skip} skipped ==");
            return _fail;
        }

        // ---------- test plumbing ----------

        private static void Test(string name, Action body)
        {
            try { body(); _pass++; Console.WriteLine($"[PASS] {name}"); }
            catch (Exception ex) { _fail++; Console.WriteLine($"[FAIL] {name}: {ex.Message}"); }
        }

        private static void RealFileTest(string name, string fixturesDir, string relative, Action<byte[]> body)
        {
            if (fixturesDir == null) { _skip++; Console.WriteLine($"[SKIP] {name} (no --fixtures)"); return; }
            var path = Path.Combine(fixturesDir, relative.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) { _skip++; Console.WriteLine($"[SKIP] {name} (missing {relative})"); return; }
            try { body(File.ReadAllBytes(path)); _pass++; Console.WriteLine($"[PASS] {name}"); }
            catch (Exception ex) { _fail++; Console.WriteLine($"[FAIL] {name}: {ex.Message}"); }
        }

        // A Test that needs one fixture file on disk but loads it through its own code path
        // (decrypt/parse/directory walk). Skips cleanly, like RealFileTest, when --fixtures
        // was not given or the file is absent, so a data-less checkout reports SKIP instead
        // of crashing inside Path.Combine.
        private static void FixtureTest(string name, string fixturesDir, string relative, Action body)
        {
            if (fixturesDir == null) { _skip++; Console.WriteLine($"[SKIP] {name} (no --fixtures)"); return; }
            var path = Path.Combine(fixturesDir, relative.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) { _skip++; Console.WriteLine($"[SKIP] {name} (missing {relative})"); return; }
            Test(name, body);
        }

        private static void RealFileTestTwo(string name, string fixturesDir, string relativeA,
            string relativeB, Action<byte[], byte[]> body)
        {
            if (fixturesDir == null) { _skip++; Console.WriteLine($"[SKIP] {name} (no --fixtures)"); return; }
            var a = Path.Combine(fixturesDir, relativeA.Replace('/', Path.DirectorySeparatorChar));
            var b = Path.Combine(fixturesDir, relativeB.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(a)) { _skip++; Console.WriteLine($"[SKIP] {name} (missing {relativeA})"); return; }
            if (!File.Exists(b)) { _skip++; Console.WriteLine($"[SKIP] {name} (missing {relativeB})"); return; }
            try { body(File.ReadAllBytes(a), File.ReadAllBytes(b)); _pass++; Console.WriteLine($"[PASS] {name}"); }
            catch (Exception ex) { _fail++; Console.WriteLine($"[FAIL] {name}: {ex.Message}"); }
        }

        // Compares PtCodec.Decrypt against a folder of pt.exe reference decryptions (oracle_batch/enc
        // vs oracle_batch/decrypted, matched by filename). SKIPs cleanly if the batch was not generated.
        private static void BatchOracleTest(string name, string fixturesDir)
        {
            var encDir = fixturesDir == null ? null
                : Path.Combine(fixturesDir, "_analysis_scratch", "oracle_batch", "enc");
            var decDir = fixturesDir == null ? null
                : Path.Combine(fixturesDir, "_analysis_scratch", "oracle_batch", "decrypted");
            if (encDir == null || !Directory.Exists(encDir) || !Directory.Exists(decDir))
            { _skip++; Console.WriteLine($"[SKIP] {name} (oracle_batch not generated)"); return; }

            try
            {
                int total = 0, matched = 0;
                foreach (var encPath in Directory.GetFiles(encDir, "*.pt"))
                {
                    var oraclePath = Path.Combine(decDir, Path.GetFileName(encPath));
                    if (!File.Exists(oraclePath)) continue;
                    total++;
                    var mine = PtCodec.Decrypt(File.ReadAllBytes(encPath));
                    if (Equal(mine, File.ReadAllBytes(oraclePath))) matched++;
                    else throw new Exception($"byte mismatch on {Path.GetFileName(encPath)}");
                }
                AssertTrue(total >= 20, $"expected a meaningful batch, only checked {total}");
                AssertTrue(matched == total, $"{matched}/{total} matched the oracle");
                _pass++;
                Console.WriteLine($"[PASS] {name} ({matched}/{total} files match pt.exe byte-for-byte)");
            }
            catch (Exception ex) { _fail++; Console.WriteLine($"[FAIL] {name}: {ex.Message}"); }
        }

        private static bool Equal(byte[] x, byte[] y)
        {
            if (x == null || y == null || x.Length != y.Length) return false;
            for (int i = 0; i < x.Length; i++) if (x[i] != y[i]) return false;
            return true;
        }

        // A minimal "decrypted" PT: 24-byte plaintext header + a body whose first u32 is <= 10 (so the
        // codec's auto-detect picks the encrypt direction). Content is arbitrary but fixed/deterministic.
        private static byte[] SyntheticDecryptedPt()
        {
            var d = new byte[0x18 + 64];
            var magic = System.Text.Encoding.ASCII.GetBytes("PTFF");
            Array.Copy(magic, d, 4);
            for (int i = 4; i < 0x18; i++) d[i] = (byte)(i * 7 + 3); // deterministic header filler
            d[0x18] = 4; // first body word = 4 (<= 10)
            for (int i = 0x1C; i < d.Length; i++) d[i] = (byte)(i & 0xFF);
            return d;
        }

        private static void AssertFormat(FormatDetectionResult r, ChartFormat expected)
        {
            if (r == null) throw new Exception("result was null");
            if (r.Format != expected)
                throw new Exception($"expected {expected}, got {r.Format} [{r}]");
        }

        private static void AssertTrue(bool cond, string msg)
        {
            if (!cond) throw new Exception(msg);
        }

        private static void AssertThrows(ChartLoadError expected, Action body)
        {
            try { body(); }
            catch (ChartLoadException ex)
            {
                if (ex.Kind != expected) throw new Exception($"expected {expected}, got {ex.Kind}");
                return;
            }
            throw new Exception($"expected ChartLoadException({expected}) but none was thrown");
        }

        private static void AssertThrowsAny(Action body)
        {
            try { body(); }
            catch (ChartLoadException) { return; }
            throw new Exception("expected a ChartLoadException but none was thrown");
        }

        private static void AssertThrowsAnyException(Action body)
        {
            try { body(); }
            catch { return; }
            throw new Exception("expected an exception but none was thrown");
        }

        // Offset of the track's eventsCount field for a BuildTrailer(1 instrument, 1 track) layout:
        // header(8) + instrument(67) + track-preamble(skip2 + name0x40 + skip4 = 70) = 145.
        private const int EventsCountOffset = 8 + 67 + 70;

        private static uint TrailerOffset(byte[] data)
        {
            return (uint)(data[4] | (data[5] << 8) | (data[6] << 16) | (data[7] << 24));
        }

        private static string Sha256(byte[] data)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(data));
        }

        private static void PatchU16(byte[] b, uint off, ushort v) => WriteU16(b, (int)off, v);
        private static void PatchU32(byte[] b, int off, uint v) => WriteU32(b, off, v);

        // ---------- synthetic fixtures ----------

        private static byte[] SyntheticDecryptedPtff()
        {
            var b = new byte[0x30];
            b[0] = (byte)'P'; b[1] = (byte)'T'; b[2] = (byte)'F'; b[3] = (byte)'F';
            b[4] = 1; // version
            // EZTR track magic at 0x18
            b[0x18] = 0x45; b[0x19] = 0x5A; b[0x1A] = 0x54; b[0x1B] = 0x52;
            return b;
        }

        private static byte[] SyntheticEncryptedPtff()
        {
            var b = new byte[256];
            b[0] = (byte)'P'; b[1] = (byte)'T'; b[2] = (byte)'F'; b[3] = (byte)'F';
            for (int i = 4; i < b.Length; i++) b[i] = 0xAA; // body flag @0x18 = 0xAAAAAAAA (>10); no EZTR bytes
            return b;
        }

        private static byte[] SyntheticTrailer()
        {
            // header(12) + body(8) + trailer(26); trailer offset = 20
            var b = new byte[12 + 8 + 26];
            uint off = 20;
            WriteU32(b, 0, 0);      // sign
            WriteU32(b, 4, off);    // num (trailer offset)
            WriteU32(b, 8, 0);      // pos
            int t = (int)off;
            WriteU16(b, t + 0, 1);      // insCnt
            WriteU16(b, t + 2, 1);      // trackCnt
            WriteU16(b, t + 4, 192);    // tickPerMinute
            // remaining trailer bytes left zero
            return b;
        }

        private static byte[] SyntheticTrailerBadOffset()
        {
            var b = new byte[64];
            WriteU32(b, 0, 0);
            WriteU32(b, 4, 999999); // offset past EOF
            return b;
        }

        private static byte[] SyntheticTrailerTruncated()
        {
            var b = new byte[40];
            WriteU32(b, 0, 0);
            WriteU32(b, 4, (uint)(b.Length - 10)); // offset leaves < 26 bytes for the trailer
            return b;
        }

        private static byte[] Garbage()
        {
            var b = new byte[128];
            for (int i = 0; i < b.Length; i++) b[i] = (byte)(0x11 + (i * 7 % 200));
            // ensure it doesn't start with a known signature
            b[0] = 0x42; b[1] = 0x11; b[2] = 0x77; b[3] = 0x03;
            return b;
        }

        private static byte[] Xml()
        {
            return System.Text.Encoding.UTF8.GetBytes("<?xml version=\"1.0\"?><pattern></pattern>");
        }

        private static IEnumerable<byte[]> HostileInputs()
        {
            yield return new byte[0];
            yield return new byte[] { 0 };
            yield return new byte[] { 0x50, 0x54, 0x46, 0x46 };          // PTFF only, nothing else
            yield return new byte[] { 0, 0, 0, 0 };                       // zero sign, no more
            yield return new byte[] { 0, 0, 0, 0, 0xFF, 0xFF, 0xFF, 0xFF }; // zero sign + huge offset
            yield return Garbage();
        }

        private static void WriteU32(byte[] b, int off, uint v)
        {
            b[off] = (byte)v; b[off + 1] = (byte)(v >> 8); b[off + 2] = (byte)(v >> 16); b[off + 3] = (byte)(v >> 24);
        }

        private static void WriteU16(byte[] b, int off, ushort v)
        {
            b[off] = (byte)v; b[off + 1] = (byte)(v >> 8);
        }

        /// <summary>
        /// Builds a structurally valid trailer-format chart with 1 instrument ("1_dr_001.wav") and 1
        /// track carrying the given event codes (payload zero-filled). Layout mirrors what
        /// TrailerChartReader expects: instruments begin at offset 8, the 26-byte metadata trailer sits
        /// at EOF, and the offset word at 0x04 points to it.
        /// </summary>
        private static byte[] BuildTrailer(byte[] eventCodes, bool fillInstrumentName = false)
        {
            var b = new List<byte>();
            AddU32(b, 0);                 // sign
            AddU32(b, 0);                 // num placeholder (patched below), index 4

            // instrument table (starts at offset 8)
            AddU16(b, 5);                 // insNo
            b.Add(0);                     // skip 1
            AddName(b, "1_dr_001.wav", 0x40, fillInstrumentName);

            // one track
            AddU16(b, 0);                 // skip 2
            AddName(b, "track0", 0x40, false);
            AddU32(b, 0);                 // skip 4
            AddU32(b, (uint)eventCodes.Length); // eventsCount
            foreach (var code in eventCodes)
            {
                AddU32(b, 0);             // tick
                b.Add(code);              // event code
                for (int i = 0; i < 8; i++) b.Add(0); // payload
            }

            uint num = (uint)b.Count;     // trailer offset = current EOF

            // 26-byte metadata trailer
            AddU16(b, 1);                 // insCnt
            AddU16(b, 1);                 // trackCnt
            AddU16(b, 192);               // tickPerMinute
            AddF32(b, 120f);              // tempo
            AddU32(b, 0);                 // tick1
            AddF32(b, 0f);                // playTime
            AddU32(b, 0);                 // endTick
            AddU32(b, 0);                 // reserved

            var arr = b.ToArray();
            WriteU32(arr, 4, num);
            return arr;
        }

        private static void AddU32(List<byte> b, uint v)
        {
            b.Add((byte)v); b.Add((byte)(v >> 8)); b.Add((byte)(v >> 16)); b.Add((byte)(v >> 24));
        }

        private static void AddU16(List<byte> b, ushort v)
        {
            b.Add((byte)v); b.Add((byte)(v >> 8));
        }

        private static void AddF32(List<byte> b, float v)
        {
            b.AddRange(BitConverter.GetBytes(v));
        }

        private static void AddName(List<byte> b, string s, int len, bool fillNoTerminator)
        {
            var raw = System.Text.Encoding.ASCII.GetBytes(s);
            for (int i = 0; i < len; i++)
            {
                if (i < raw.Length) b.Add(raw[i]);
                else b.Add(fillNoTerminator ? (byte)'A' : (byte)0);
            }
        }
    }
}
