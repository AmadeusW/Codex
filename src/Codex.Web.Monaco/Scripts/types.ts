/// <reference path="../node_modules/@types/jquery/index.d.ts"/>

type StringMap = { [key:string]:string; };

interface ICodexWebPage {
    findAllReferences(projectId: string, symbol: string): Promise<void>;

    showDocumentOutline(projectId: string, filePath: string);

    showProjectExplorer(projectId: string);

    showNamespaceExplorer(projectId: string);

    getUrlForLine(lineNumber: number): string;
}

interface ICodexEditor {
    openFile(sourceFile: SourceFile, targetLocation?: TargetEditorLocation): Promise<void> | void;

    navigateTo(targetLocation: TargetEditorLocation);
}

interface ICodexWebServer {
    getSourceFile(projectId: string, filePath: string): Promise<SourceFile>;
    getToolTip(projectId: string, symbol: string): Promise<ToolTip>;
    getDefinitionLocation(projectId: string, symbol: string): Promise<SourceFileOrView>;
}

class CodexWebServer implements ICodexWebServer {
    getDefinitionLocation(projectId: string, symbol: string): Promise<SourceFileOrView> {
        return rpc.getDefinitionLocation(projectId, symbol);
    }

    getToolTip(projectId: string, symbol: string): Promise<ToolTip> {
        return rpc.getToolTip(projectId, symbol);
    }

    getSourceFile(projectId: string, filePath: string): Promise<SourceFile> {
        return rpc.getSourceFileContents(projectId, filePath);
    }
}

function notImplemented(): Error {
    throw new Error("Method not implemented.");
}

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

interface SourceFile {
    filePath: string;
    webLink: string;
    repoRelativePath: string;
    projectId: string;

    contents: string;
    // TODO: move span out?
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

type SourceFileOrView = string | SourceFile;

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