namespace GoFish.DataAccess.VisualFoxPro
{
    public class ProjectEntry
    {
        public string Name { get; set; }
        public char Type { get; set; }
        public long Id { get; set; }
        public long TimeStamp { get; set; }
        public string Outfile { get; set; }
        public string HomeDir { get; set; }
        public bool Exclude { get; set; }
        public bool MainProg { get; set; }
        public bool SaveCode { get; set; }
        public bool Debug { get; set; }
        public bool Encrypt { get; set; }
        public bool NoLogo { get; set; }
        public byte CmntStyle { get; set; }
        public int ObjRev { get; set; }
        public string DevInfo { get; set; }
        public string Symbols { get; set; }
        public string Object { get; set; }
        public int CkVal { get; set; }
        public int CPid { get; set; }
        public string OSType { get; set; }
        public string OSCreator { get; set; }
        public string Comments { get; set; }
        public string Reserved1 { get; set; }
        public string Reserved2 { get; set; }
        public string SccData { get; set; }
        public bool Local { get; set; }
        public string Key { get; set; }
        public string User { get; set; }
    }
}
