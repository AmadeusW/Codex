/// <reference path="../node_modules/@types/jquery/index.d.ts"/>

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
