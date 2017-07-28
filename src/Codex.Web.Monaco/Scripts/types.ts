/// <reference path="../node_modules/@types/jquery/index.d.ts"/>

interface Span {
    position: number;
    length: number;
}

interface LineSpan extends Span {
    line: number;
    column: number;
}

interface SymbolSpan {
    symbol: string;
    projectId: string;
    span: Span;
}

interface SegmentModel {
    definitions: SymbolSpan[];
    references: SymbolSpan[];
}

interface ClassificationSpan extends Span {
    name: string;
}

interface SourceFileContentsModel {
    filePath: string;
    webLink: string;
    repoRelativePath: string;
    projectId: string;

    contents: string;
    span: LineSpan;
    segments: SegmentModel[];
    classifications: ClassificationSpan[];
    documentSymbols: SymbolInformation[];
    // Width int characters of segments
    segmentLength: number;
}

interface SymbolInformation {
    name: string;
    containerName: string;
    symbolKind: string;
    span: Span;
}

type SourceFileOrView = string | SourceFileContentsModel;

interface ToolTip {
    projectId: string;
    fullName: string;
    comment: string;
    symbolKind: string;
    typeName: string;
    definitionText: string;
}

function generateToolTipHeader(toolTip: ToolTip): monaco.MarkedString {
    let value = toolTip.definitionText;
    // Definitions could have a trailing ','. Removing it.
    if (value[value.length - 1] === ',') {
        value = value.substr(0, value.length - 1);
    }

    // TODO: not safe.
    return { language: 'csharp', value: value };
}

function generateToolTipBody(toolTip: ToolTip): monaco.MarkedString[] {

    // This could be a hyperlink.
    let result: string = `Project **${toolTip.projectId}**`;

    if (toolTip.comment) {
        result += `  \r\n*${extractSummaryText(toolTip.comment).trim()}*`;
    }

    return [result];
}

const summaryStartTag = /<summary>/i;
const summaryEndTag = /<\/summary>/i;

function extractSummaryText(xmlDocComment: string): string {
    if (!xmlDocComment) {
        return xmlDocComment;
    }

    let summary = xmlDocComment;

    let startIndex = summary.search(summaryStartTag);
    if (startIndex < 0) {
        return summary;
    }

    summary = summary.slice(startIndex + '<summary>'.length);

    let endIndex = summary.search(summaryEndTag);
    if (endIndex < 0) {
        return summary;
    }

    return summary.slice(0, endIndex);
}