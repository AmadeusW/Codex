/// <reference path="../node_modules/monaco-editor/monaco.d.ts"/>
/// <reference path="types.ts"/>

interface IViewModelPart {
    appendParams(queryParams: StringMap);
}

interface IPaneViewModel extends IViewModelPart {
    kind: string;
}

interface RightPaneViewModel extends IPaneViewModel {
    kind: 'SourceFile' | 'Overview'
}

interface IViewModel {
    right: RightPaneViewModel;
    left: ILeftPaneViewModel;
}

interface ILeftPaneViewModel extends IPaneViewModel {

}

class OverviewViewModel implements RightPaneViewModel {
    kind: 'Overview';

    appendParams(queryParams: StringMap) {
        // do nothing (overview is default)
    }
}

type LineNumber = number;// {kind: 'number'; value: number}
type Symbol = { symbolId: string, projectId: string };//kind: 'symbol'; value: string}
type TargetEditorLocation = {kind: 'line'; value: LineNumber} | {kind: 'symbol'; value: Symbol} | {kind: 'span'; value: Span};

class FileViewModel implements RightPaneViewModel {
    
    kind: 'SourceFile';
    //\
    sourceFile: SourceFile | undefined;
    projectId: string;
    filePath: string;
    targetLocation: TargetEditorLocation;

    appendParams(queryParams: StringMap) {
        queryParams['rightProject'] = this.projectId;
        queryParams['file'] = this.filePath;
        
        if (!this.targetLocation || this.targetLocation.kind === 'span') {
            return;
        }

        switch(this.targetLocation.kind) {
            case 'line':
                queryParams['line'] = this.targetLocation.value.toString();
                break;
            case 'symbol':
                queryParams['rightSymbol'] = this.targetLocation.value.symbolId;
                break;
        }
    }
}

function getUrlForViewModel(viewModel: IViewModel) {
    if (!viewModel) {
        return null;
    }

    let queryParams: StringMap = {};
    if (viewModel.left) {
        viewModel.left.appendParams(queryParams);
    }

    if (viewModel.right) {
        viewModel.right.appendParams(queryParams);
    }

    if (Object.keys(queryParams).length == 0) {
        return null;
    }

    let url = "?" + Object.keys(queryParams).map(key => `${key}=${encodeURIComponent(queryParams[key])}`).join('&');
    return url;
}