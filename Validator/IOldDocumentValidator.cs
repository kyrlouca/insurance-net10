using Shared.DataModels;

namespace Validations
{
    public interface IOldDocumentValidator
    {
        DbValue GetCellValueFromDbNew(int docId, string tableCode, string row, string col);
        DbValue GetCellValueFromOneSheetDb(string tableCode, int sheetId, string row, string col);
        TemplateSheetFact GetFact(string sheetCode, string row, string col);
        void UpdateTermRowCol(RuleTerm term, string scopeTableCode, ScopeRangeAxis scopeAxis, string rowCol);
        int ValidateDocument();
    }
}