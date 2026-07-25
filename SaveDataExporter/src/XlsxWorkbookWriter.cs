using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace SaveDataExporter;

/// <summary>
/// 工作表模型：名称与行集合。
/// </summary>
internal sealed class XlsxSheet {
    /// <summary>
    /// 创建命名工作表。
    /// </summary>
    /// <param name="name">工作表显示名（写入 workbook.xml）。</param>
    /// <param name="rows">按行序排列的单元格数据。</param>
    public XlsxSheet(string name, IReadOnlyList<XlsxRow> rows) {
        Name = name;
        Rows = rows;
    }

    /// <summary>
    /// 工作表显示名。
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 按行序排列的单元格数据。
    /// </summary>
    public IReadOnlyList<XlsxRow> Rows { get; }
}

/// <summary>
/// 工作表中的一行单元格。
/// </summary>
internal sealed class XlsxRow {
    /// <summary>
    /// 使用单元格值序列创建行。
    /// </summary>
    /// <param name="cells">从左到右的单元格值；null 表示空单元格。</param>
    public XlsxRow(IReadOnlyList<object> cells) {
        Cells = cells;
    }

    /// <summary>
    /// 从左到右的单元格值。
    /// </summary>
    public IReadOnlyList<object> Cells { get; }
}

/// <summary>
/// 将工作表模型序列化为 OOXML xlsx（zip + worksheet XML），不依赖 ClosedXML。
/// </summary>
internal static class XlsxWorkbookWriter {
    /// <summary>
    /// 无 BOM 的 UTF-8 编码，确保 OOXML 内部 XML 文件开头不含字节顺序标记。
    /// </summary>
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

    /// <summary>
    /// 将多个工作表写入指定路径的 xlsx 文件（覆盖已存在文件）。
    /// </summary>
    /// <param name="path">目标文件路径。</param>
    /// <param name="sheets">按顺序写入的工作表；sheet1..n 与 workbook 顺序一致。</param>
    public static void Save(string path, IReadOnlyList<XlsxSheet> sheets) {
        using FileStream fileStream = new(path, FileMode.Create, FileAccess.Write, FileShare.None);
        using ZipArchive archive = new(fileStream, ZipArchiveMode.Create);

        AddEntry(archive, "[Content_Types].xml", BuildContentTypes(sheets.Count));
        AddEntry(archive, "_rels/.rels", BuildRootRelationships());
        AddEntry(archive, "docProps/app.xml", BuildAppProperties());
        AddEntry(archive, "docProps/core.xml", BuildCoreProperties());
        AddEntry(archive, "xl/workbook.xml", BuildWorkbook(sheets));
        AddEntry(archive, "xl/_rels/workbook.xml.rels", BuildWorkbookRelationships(sheets.Count));
        for (int i = 0; i < sheets.Count; i++) {
            AddEntry(archive, $"xl/worksheets/sheet{i + 1}.xml", BuildWorksheet(sheets[i]));
        }
    }

    /// <summary>
    /// 向 ZIP 归档添加一个文本条目，使用无 BOM UTF-8 写入。
    /// </summary>
    /// <param name="archive">目标 ZipArchive。</param>
    /// <param name="path">归档内路径，如 "xl/workbook.xml"。</param>
    /// <param name="content">XML 字符串内容。</param>
    private static void AddEntry(ZipArchive archive, string path, string content) {
        ZipArchiveEntry entry = archive.CreateEntry(path, System.IO.Compression.CompressionLevel.Optimal);
        using Stream stream = entry.Open();
        using StreamWriter writer = new(stream, Utf8NoBom);
        writer.Write(content);
    }

    /// <summary>
    /// 构建 [Content_Types].xml，声明默认扩展名与各覆盖部件的 MIME 类型。
    /// </summary>
    /// <param name="sheetCount">工作表数量，决定 worksheet Override 条目数。</param>
    /// <returns>[Content_Types].xml 字符串。</returns>
    private static string BuildContentTypes(int sheetCount) {
        StringBuilder builder = new();
        builder.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        builder.Append("""<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">""");
        builder.Append("""<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>""");
        builder.Append("""<Default Extension="xml" ContentType="application/xml"/>""");
        builder.Append("""<Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>""");
        for (int i = 1; i <= sheetCount; i++) {
            builder.Append($"""<Override PartName="/xl/worksheets/sheet{i}.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>""");
        }
        builder.Append("""<Override PartName="/docProps/core.xml" ContentType="application/vnd.openxmlformats-package.core-properties+xml"/>""");
        builder.Append("""<Override PartName="/docProps/app.xml" ContentType="application/vnd.openxmlformats-officedocument.extended-properties+xml"/>""");
        builder.Append("</Types>");
        return builder.ToString();
    }

    /// <summary>
    /// 构建 ZIP 根 _rels/.rels，声明 workbook、core-properties 和 extended-properties 的关系。
    /// </summary>
    /// <returns>_rels/.rels XML 字符串。</returns>
    private static string BuildRootRelationships() {
        return """
               <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
               <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                 <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
                 <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties" Target="docProps/core.xml"/>
                 <Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties" Target="docProps/app.xml"/>
               </Relationships>
               """;
    }

    /// <summary>
    /// 构建 docProps/app.xml，标记应用程序名为 SaveDataExporter。
    /// </summary>
    /// <returns>app.xml XML 字符串。</returns>
    private static string BuildAppProperties() {
        return """
               <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
               <Properties xmlns="http://schemas.openxmlformats.org/officeDocument/2006/extended-properties" xmlns:vt="http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes">
                 <Application>SaveDataExporter</Application>
               </Properties>
               """;
    }

    /// <summary>
    /// 构建 docProps/core.xml，包含创建者与创建/修改时间戳（UTC）。
    /// </summary>
    /// <returns>core.xml XML 字符串。</returns>
    private static string BuildCoreProperties() {
        string now = XmlConvert.ToString(DateTime.UtcNow, XmlDateTimeSerializationMode.Utc);
        return $"""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <cp:coreProperties xmlns:cp="http://schemas.openxmlformats.org/package/2006/metadata/core-properties" xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:dcterms="http://purl.org/dc/terms/" xmlns:dcmitype="http://purl.org/dc/dcmitype/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
                  <dc:creator>SaveDataExporter</dc:creator>
                  <cp:lastModifiedBy>SaveDataExporter</cp:lastModifiedBy>
                  <dcterms:created xsi:type="dcterms:W3CDTF">{now}</dcterms:created>
                  <dcterms:modified xsi:type="dcterms:W3CDTF">{now}</dcterms:modified>
                </cp:coreProperties>
                """;
    }

    /// <summary>
    /// 构建 xl/workbook.xml，按顺序定义各工作表的名称与关系 ID。
    /// 工作表名通过 EscapeAttribute 转义以防范 XML 注入。
    /// </summary>
    /// <param name="sheets">工作表列表，顺序与关系 ID 对应。</param>
    /// <returns>workbook.xml 字符串。</returns>
    private static string BuildWorkbook(IReadOnlyList<XlsxSheet> sheets) {
        StringBuilder builder = new();
        builder.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        builder.Append("""<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets>""");
        for (int i = 0; i < sheets.Count; i++) {
            builder.Append($"""<sheet name="{EscapeAttribute(sheets[i].Name)}" sheetId="{i + 1}" r:id="rId{i + 1}"/>""");
        }
        builder.Append("</sheets></workbook>");
        return builder.ToString();
    }

    /// <summary>
    /// 构建 xl/_rels/workbook.xml.rels，将 rId1..n 映射到 worksheets/sheet1..n.xml。
    /// </summary>
    /// <param name="sheetCount">工作表数量。</param>
    /// <returns>workbook.xml.rels XML 字符串。</returns>
    private static string BuildWorkbookRelationships(int sheetCount) {
        StringBuilder builder = new();
        builder.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        builder.Append("""<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">""");
        for (int i = 1; i <= sheetCount; i++) {
            builder.Append($"""<Relationship Id="rId{i}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet{i}.xml"/>""");
        }
        builder.Append("</Relationships>");
        return builder.ToString();
    }

    /// <summary>
    /// 构建单个 xl/worksheets/sheet{n}.xml，将行/单元格序列化为 &lt;sheetData&gt;。
    /// 单元格（除 null 外）自动推断 inlineStr 或数值类型。
    /// </summary>
    /// <param name="sheet">工作表模型。</param>
    /// <returns>worksheet XML 字符串。</returns>
    private static string BuildWorksheet(XlsxSheet sheet) {
        StringBuilder builder = new();
        builder.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        builder.Append("""<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>""");
        for (int rowIndex = 0; rowIndex < sheet.Rows.Count; rowIndex++) {
            XlsxRow row = sheet.Rows[rowIndex];
            int excelRow = rowIndex + 1;
            builder.Append($"""<row r="{excelRow}">""");
            for (int columnIndex = 0; columnIndex < row.Cells.Count; columnIndex++) {
                AppendCell(builder, excelRow, columnIndex + 1, row.Cells[columnIndex]);
            }
            builder.Append("</row>");
        }
        builder.Append("</sheetData></worksheet>");
        return builder.ToString();
    }

    /// <summary>
    /// 向 worksheet &lt;sheetData&gt; 的 StringBuilder 追加一个单元格 XML 元素。
    /// null 值输出空标签 &lt;c/&gt;；数值类型按 CultureInfo.InvariantCulture 序列化；
    /// 其他类型以 inlineStr 方式写入转义文本。
    /// </summary>
    /// <param name="builder">正在构建的 worksheet XML StringBuilder。</param>
    /// <param name="row">Excel 1-based 行号。</param>
    /// <param name="column">Excel 1-based 列号。</param>
    /// <param name="value">单元格值；null 表示空单元格。</param>
    private static void AppendCell(StringBuilder builder, int row, int column, object value) {
        string reference = ColumnName(column) + row.ToString(CultureInfo.InvariantCulture);
        if (value == null) {
            builder.Append($"""<c r="{reference}"/>""");
            return;
        }

        switch (value) {
            case int intValue:
                builder.Append($"""<c r="{reference}"><v>{intValue.ToString(CultureInfo.InvariantCulture)}</v></c>""");
                break;
            case long longValue:
                builder.Append($"""<c r="{reference}"><v>{longValue.ToString(CultureInfo.InvariantCulture)}</v></c>""");
                break;
            case float floatValue:
                builder.Append($"""<c r="{reference}"><v>{floatValue.ToString(CultureInfo.InvariantCulture)}</v></c>""");
                break;
            case double doubleValue:
                builder.Append($"""<c r="{reference}"><v>{doubleValue.ToString("0.###", CultureInfo.InvariantCulture)}</v></c>""");
                break;
            case decimal decimalValue:
                builder.Append($"""<c r="{reference}"><v>{decimalValue.ToString(CultureInfo.InvariantCulture)}</v></c>""");
                break;
            default:
                builder.Append($"""<c r="{reference}" t="inlineStr"><is><t>{EscapeText(value.ToString() ?? "")}</t></is></c>""");
                break;
        }
    }

    /// <summary>
    /// 将 1-based 列号转为 Excel 列名（A, B, ..., Z, AA, AB...）。
    /// </summary>
    /// <param name="column">1-based 列号。</param>
    /// <returns>Excel 列名字符串。</returns>
    private static string ColumnName(int column) {
        StringBuilder builder = new();
        while (column > 0) {
            column--;
            builder.Insert(0, (char)('A' + column % 26));
            column /= 26;
        }
        return builder.ToString();
    }

    /// <summary>
    /// 对 inlineStr &lt;t&gt; 文本内容做 XML 转义。
    /// </summary>
    /// <param name="value">原始文本。</param>
    /// <returns>XML 安全字符串。</returns>
    private static string EscapeText(string value) {
        return SecurityElementEscape(value);
    }

    /// <summary>
    /// 对 XML 属性值做 XML 转义。
    /// </summary>
    /// <param name="value">原始属性值。</param>
    /// <returns>XML 安全字符串。</returns>
    private static string EscapeAttribute(string value) {
        return SecurityElementEscape(value);
    }

    /// <summary>
    /// 对字符串做 & < > " ' 五字符 XML 转义，用于 OOXML 文本内容与属性值。
    /// 组合了 EscapeText 与 EscapeAttribute 的共同实现。
    /// </summary>
    /// <param name="value">原始字符串。</param>
    /// <returns>XML 安全字符串。</returns>
    private static string SecurityElementEscape(string value) {
        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }
}
