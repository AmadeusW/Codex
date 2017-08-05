/// <reference path="../node_modules/monaco-editor/monaco.d.ts"/>
/// <reference path="types.ts"/>

interface IViewModelPart {
    updateState(state: CodexWebState);
}

interface IPaneViewModel extends IViewModelPart {
    kind: string;
}

type RightPaneViewModel = FileViewModel | OverviewViewModel;

interface IViewModel {
    right: RightPaneViewModel;
    left: ILeftPaneViewModel;
}

interface ILeftPaneViewModel extends IPaneViewModel {

}

class OverviewViewModel {
    kind: 'Overview';
}

type LineNumber = number;// {kind: 'number'; value: number}
type Symbol = { symbolId: string, projectId: string };//kind: 'symbol'; value: string}
type TargetEditorLocation = {kind: 'line'; value: LineNumber} | {kind: 'symbol'; value: Symbol} | {kind: 'span'; value: Span};

interface FileViewModel {
    
    kind: 'SourceFile';
    //\
    sourceFile: SourceFile | undefined;
    projectId: string;
    filePath: string;
    targetLocation: TargetEditorLocation;
}

//function getUrlForViewModel(viewModel: IViewModel) {
//    if (!viewModel) {
//        return null;
//    }

//    let queryParams: StringMap = {};
//    if (viewModel.left) {
//        viewModel.left.appendParams(queryParams);
//    }

//    if (viewModel.right) {
//        viewModel.right.appendParams(queryParams);
//    }

//    if (Object.keys(queryParams).length == 0) {
//        return null;
//    }

//    let url = "?" + Object.keys(queryParams).map(key => `${key}=${encodeURIComponent(queryParams[key])}`).join('&');
//    return url;
//}