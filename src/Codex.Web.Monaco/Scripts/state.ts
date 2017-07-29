/// <reference path="../node_modules/@types/jquery/index.d.ts"/>
/// <reference path="../node_modules/monaco-editor/monaco.d.ts"/>
/// <reference path="types.ts" />
/// <reference path="codexEditor.ts" />

namespace state {
    export let codexWebRootPrefix = "";
    export let defaultWindowTitle = "Index";
    export let WebPage: CodexWebPage;
    export let codexEditor: CodexEditor;
    export let ctrlClickLinkDecorations: string[];
    
    export let currentState: CodexWebState;
    export let searchBox: any;
    export let lastSearchString: any;
    export let selectedFile;
    export let rightPaneIsEditor: boolean;

    export let lastQuery: string = null;
    export let searchTimerID: number = -1;

    export let keyboardSelectedResultGroup : any = null;
    export let keyboardSelectedResult : any = null;
    export let keyboardSelectedUIElement : any = null;
}

type RightPaneContent = 'file' | 'line' | 'symbol' | 'overview' | 'about';
type LeftPaneContent = 'outline' | 'search' | 'project' | 'references' | 'namespaces';

interface CodexWebState {
    leftProjectId: string;
    rightProjectId: string;
    filePath: string;
    leftSymbolId: string;
    projectScope: string;
    rightSymbolId: string;
    lineNumber: number;
    searchText: string;
    windowTitle: string;
    leftPaneContent: LeftPaneContent;
    rightPaneContent: RightPaneContent;
}

function main() {
    state.WebPage = new CodexWebPage();
    state.WebPage.setViewModel({left: undefined, right: {kind: 'Overview'}});
}