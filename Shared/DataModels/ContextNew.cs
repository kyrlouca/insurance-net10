namespace Shared.DataModels;
using System.Collections.Generic;


public class ContextNew
{
	public int InstanceId { get; set; }
	public int ContextId { get; set; }
	public string ContextXbrlId { get; set; }
	public string Signature { get; private set; }
	public int TableId { get; set; }	

}

