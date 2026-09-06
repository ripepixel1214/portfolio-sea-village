using UnityEngine;

[System.Serializable]
public class SheetMapping
{
    [Tooltip("Google Spreadsheet 내 시트(탭) 이름")]
    public string sheetName;

    [Tooltip("저장될 로컬 CSV 파일명 (예: Item.csv)")]
    public string csvFileName;

    [Tooltip("다운로드 대상 여부")]
    public bool enabled = true;

    public SheetMapping() { }

    public SheetMapping(string sheetName, string csvFileName)
    {
        this.sheetName = sheetName;
        this.csvFileName = csvFileName;
        this.enabled = true;
    }
}
