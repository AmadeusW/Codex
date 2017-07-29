/// <reference path="../node_modules/@types/jquery/index.d.ts"/>

interface ICodexWebPage {
    findAllReferences(projectId: string, symbol: string): Promise<void>;

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

class CodexWebPage implements ICodexWebPage {
    private editor: CodexEditor;
    private server: CodexWebServer;

    private right: RightPaneViewModel;

    private editorPane: HTMLElement;

    setViewModel(viewModel: IViewModel) {
        if (viewModel.right) {
            this.setRightPane(viewModel.right);
        }

        if (viewModel.left) {
            this.setLeftPane(viewModel.left);
        }
    }

    private async setRightPane(viewModel: RightPaneViewModel) {
        if (this.right || this.right.kind !== viewModel.kind) {
            switch(viewModel.kind) {
                case 'SourceFile':
                    this.editor = await CodexEditor.createAsync(this.server, this, this.editorPane);
                    break;
                case 'Home':
                    break;
            }
        }

        if (viewModel.kind === 'SourceFile') {
            
        }
    }

    private async applyFileViewModel(newModel: FileViewModel, oldModel: FileViewModel) {
        if (newModel.filePath !== oldModel.filePath || newModel.projectId !== oldModel.projectId) {
            const sourceFile = newModel.sourceFile || await this.server.getSourceFile(newModel.projectId, newModel.filePath);
            this.editor.openFile(sourceFile, newModel.targetLocation);
            return;
        }
        
        if (newModel.targetLocation !== oldModel.targetLocation) {
            this.editor.navigateTo(newModel.targetLocation);
        }
    }

    private setLeftPane(viewModel: ILeftPaneViewModel) {
        throw notImplemented();
    }

    getUrlForLine(lineNumber: number): string {
        var newState = jQuery.extend({}, state.currentState, { lineNumber: lineNumber, rightPaneContent: 'line' });
        return getUrlForState(newState);
    }

    async findAllReferences(projectId: string, symbol: string): Promise<void> {
        const html = await rpc.getFindAllReferencesHtml(projectId, symbol);
        updateReferences(html);
    }
}

function notImplemented(): Error {
    throw new Error("Method not implemented.");
}

interface IViewModel {
    right: RightPaneViewModel;
    left: ILeftPaneViewModel;
}

interface ILeftPaneViewModel {

}

interface HomeViewModel {
    kind: 'Home';
}

type LineNumber = number;// {kind: 'number'; value: number}
type Symbol = { symbolId: string, projectId: string };//kind: 'symbol'; value: string}
type TargetEditorLocation = {kind: 'number'; value: LineNumber} | {kind: 'symbol'; value: Symbol} | {kind: 'span'; value: Span};

interface FileViewModel {
    kind: 'SourceFile';
    //\
    sourceFile: SourceFile | undefined;
    projectId: string;
    filePath: string;
    targetLocation: TargetEditorLocation;
}

type RightPaneViewModel = HomeViewModel | FileViewModel;

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