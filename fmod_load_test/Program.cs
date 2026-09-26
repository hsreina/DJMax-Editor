using System;
using System.IO;
using System.Threading;
using FMODEX;

class FmodLoadTest
{
    static int Main(string[] args)
    {
        string dir = args[0];
        var files = File.ReadAllLines(args[1]); // list of ogg file names to test

        FMODEX.System system = null;
        ERRCHECK(Factory.System_Create(ref system));
        int numdrivers = 0;
        ERRCHECK(system.getNumDrivers(ref numdrivers));
        if (numdrivers == 0)
            ERRCHECK(system.setOutput(OUTPUTTYPE.NOSOUND));
        else
        {
            FMODEX.CAPS caps = FMODEX.CAPS.NONE;
            int controlpaneloutput = 0;
            SPEAKERMODE speakermode = SPEAKERMODE.STEREO;
            ERRCHECK(system.getDriverCaps(0, ref caps, ref controlpaneloutput, ref speakermode));
            ERRCHECK(system.setSpeakerMode(speakermode));
        }
        ERRCHECK(system.setDSPBufferSize(512, 4));
        system.setSoftwareFormat(44100, SOUND_FORMAT.PCMFLOAT, 0, 0, DSP_RESAMPLER.LINEAR);
        ERRCHECK(system.init(256, INITFLAGS.NORMAL, (IntPtr)null));

        int fail = 0;
        foreach (var f in files)
        {
            string path = Path.Combine(dir, f.Trim());
            FMODEX.Sound sound = null;
            RESULT r = system.createSound(
                path,
                MODE.CREATESAMPLE | MODE._2D | MODE.SOFTWARE | MODE.LOOP_OFF | MODE.ACCURATETIME,
                ref sound);

            // also measure decoded length
            string len = "-";
            uint lenMs = 0;
            if (r == RESULT.OK)
            {
                if (sound.getLength(ref lenMs, TIMEUNIT.MS) == RESULT.OK)
                    len = lenMs + " ms";
            }

            string status = r == RESULT.OK ? "OK" : "FAIL: " + r + " - " + Error.String(r);
            if (r != RESULT.OK) fail++;
            Console.WriteLine("{0,-18} {1} ({2})", f, status, len);

            // try actually playing it for real (decode path)
            if (r == RESULT.OK)
            {
                Channel chan = null;
                RESULT pr = system.playSound(CHANNELINDEX.FREE, sound, false, ref chan);
                if (pr != RESULT.OK)
                {
                    Console.WriteLine("   playSound FAIL: " + pr);
                    fail++;
                }
                else
                {
                    Thread.Sleep(120);
                    bool playing = false;
                    if (chan.getPaused(ref playing) != RESULT.OK) { }
                }
                sound.release();
            }
        }
        system.update();
        system.release();
        Console.WriteLine(fail == 0 ? "ALL OK" : fail + " FAILURES");
        return fail == 0 ? 0 : 1;
    }

    static void ERRCHECK(RESULT result)
    {
        if (result != RESULT.OK)
        {
            Console.WriteLine("FMOD error! " + result + " - " + Error.String(result));
            Environment.Exit(-1);
        }
    }
}
