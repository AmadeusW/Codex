/// <reference path="../node_modules/@types/jquery/index.d.ts"/>

interface Span {
    position: number;
    length: number;
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
    span: Span;
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
}

function generateHtmlFrom(toolTip: ToolTip) {
    return `projectId: ${toolTip.projectId}\r\n` +
        `fullName: ${toolTip.fullName}\r\n` +
        `comment: ${toolTip.comment}`;
}