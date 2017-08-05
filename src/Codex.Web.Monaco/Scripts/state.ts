/// <reference path="../node_modules/@types/jquery/index.d.ts"/>
/// <reference path="../node_modules/monaco-editor/monaco.d.ts"/>
/// <reference path="types.ts" />
/// <reference path="codexEditor.ts" />
/// <reference path="viewModel.ts" />

namespace state {
    export let codexWebRootPrefix = "";
    export let defaultWindowTitle = "Index";
    export let WebPage: CodexWebPage;
    export let codexEditor: CodexEditor;
    export let ctrlClickLinkDecorations: string[];
    
    export let currentState: CodexWebState;
    export let initialState: CodexWebState;
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

function renderState(webState: CodexWebState) {

    let right: RightPaneViewModel = undefined;
    switch (webState.rightPaneContent || 'overview') {
        case 'file':
        case 'line':
        case 'symbol':
            let targetLocation: TargetEditorLocation;
            switch (webState.rightPaneContent) {
                case 'file':
                    // Do nothing. Just navigate to file
                    break;
                case 'line':
                    targetLocation = { kind: 'line', value: webState.lineNumber };
                    break;
                case 'symbol':
                    targetLocation = {
                        kind: 'symbol', value: { projectId: webState.rightProjectId, symbolId: webState.rightSymbolId }
                    };
                    break;
            }


            right = <FileViewModel>{
                kind: 'SourceFile',
                targetLocation: targetLocation,
                filePath: webState.filePath,
                projectId: webState.rightProjectId,
            };
            break;
        case 'overview':
            right = new OverviewViewModel();
            break;
    }

    let webPage = state.WebPage;
    webPage.setViewModel({ right: right, left: undefined });
}

function codexInit(initialState: CodexWebState) {
    state.WebPage = new CodexWebPage();
    state.initialState = initialState;
}

function codexWebMain(initialState: CodexWebState) {
    ensureSearchBox();

    renderState(initialState);
    // DisplayState(initialState);
    //ReplaceCurrentState();
}

function onBodyLoad() {
    var anchor = document.location.hash;
    if (anchor && !document.location.search && document.location.pathname === "/") {
        top.location.replace("http://ddindex/" + anchor);
        return;
    }

    // https://github.com/nathancahill/Split.js
    Split(['#leftPane', '#rightPane'], {
        sizes: ['504px', 'calc(100% - 504px)'],
        gutterSize: 20,
        minSize: 1,
        cursor: 'col-resize'
    });

    ensureSearchBox();

    // TODO: why href is missing?
    var link = <any>document.getElementById("feedbackButtonLink");
    link.href = "mailto:" + "codexteam" + "@" + "microsoft" + '.' + "com";

    window.onpopstate = OnWindowPopState;
    codexWebMain(state.initialState);
}