using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.ExcelDataModels;
public readonly record struct IndexSheetListItem(string templateCode, string sheetName, string Description);
public readonly record struct IndexSheetList(string TabName, List<IndexSheetListItem> ListItems);
