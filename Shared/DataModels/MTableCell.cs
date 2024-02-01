namespace Shared.DataModels;

public class MTableCell
{
    public int CellID { get; set; }
    public int TableID { get; set; }
    public bool IsRowKey { get; set; }
    public bool IsShaded { get; set; }
    public string BusinessCode { get; set; }
    public string DatapointSignature { get; set; }
    public string DPS { get; set; }
    
    public string NoOpenDPS { get; set; }
    public string OrdinateID { get; set; }


}
