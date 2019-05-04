using System;

namespace GoFish.DataAccess
{
    public enum DbfType : byte
    {
        None = 0,
        FoxBase = 0x02,
        FoxBasePlusDBaseIII = 0x03,
        VisualFoxPro = 0x30,
        VisualFoxProAutoInc = 0x31,
        VisualFoxProVar = 0x32,
        DBaseIVTable = 0x43,
        DBaseIVSystem = 0x63,
        FoxBasePlusDBaseIIIMemo = 0x83,
        DBaseIVMemo = 0x8B,
        DBaseIVTableMemo = 0xCB,
        FoxPro2Memo = 0xF5,
        FoxBase_2 = 0xFB,
    }

    [Flags]
    public enum DbfFieldFlags : byte
    {
        None = 0,
        System = 0x01,
        Null = 0x02,
        Binary = 0x04,
        BinaryAndNull = 0x06,
        AutoInc = 0x0C,
    }

    [Flags]
    public enum DbfHeaderFlags : byte
    {
        None = 0,
        CDX = 0x01,
        Memo = 0x02,
        DBC = 0x04,
    }
}
