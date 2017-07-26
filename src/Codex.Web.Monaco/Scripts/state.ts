/// <reference path="../node_modules/@types/jquery/index.d.ts"/>
/// <reference path="../node_modules/monaco-editor/monaco.d.ts"/>

namespace state {
    export let codexWebRootPrefix = "";
    export let defaultWindowTitle = "Index";
    export let editor: monaco.editor.IStandaloneCodeEditor;
    export let editorRegistered: boolean;
    export let sourceFileModel: SourceFileContentsModel;
    export let currentTextModel;
    
    export let currentState: CodexWebState;
    export let searchBox: any;
    export let lastSearchString;
    export let selectedFile;
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
    lineNumber: string;
    searchText: string;
    windowTitle: string;
    leftPaneContent: LeftPaneContent;
    rightPaneContent: RightPaneContent;
}
