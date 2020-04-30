namespace DJMaxEditor.DJMax
{
    public enum EventAttribute
    {

        BasicNote = 0,

        /*
        Unknow_1 = 1, // from DJMax Ray
        Unknow_2 = 2, // from DJMax Ray
        Unknow_3 = 3, // from DJMax Ray
        Unknow_4 = 4, // from DJMax Ray
        */

        PressNote = 5,
        PressNoteEnd = 6,
        RepeatNote = 10,
        RepeatNoteEnd = 11,
        LongHoldNote = 12,

        /*
        Unknow_30 = 30,
        Unknow_35 = 35,

        Unknow_40 = 40, // T3 shoreline
        Unknow_45 = 45, // T3 signal
        Unknow_75 = 75, // T3 signal

        Unknow_90 = 90, // T3 shoreline



        Unknow_120 = 120, // T3 signal
        */

        VideoStart = 100, // T2/T3 video track start 
    }
}
