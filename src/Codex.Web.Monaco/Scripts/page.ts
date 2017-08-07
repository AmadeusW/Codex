/// <reference path="../node_modules/@types/jquery/index.d.ts"/>
/// <reference path="types.ts"/>
/// <reference path="viewModel.ts"/>
/// <reference path="codexEditor.ts"/>

class CodexWebPage implements ICodexWebPage {
    private editor: CodexEditor;
    private server: CodexWebServer;

    private right: RightPaneViewModel;

    private editorPane: HTMLElement;
    private editorContainerPane: HTMLElement;
    private editorTemplate: HTMLElement;
    private editorBottomPaneTemplate: HTMLElement;

    public constructor() {
        this.server = new CodexWebServer();
    }

    setViewModel(viewModel: IViewModel) {
        if (viewModel.right) {
            this.setRightPane(viewModel.right);
        }

        if (viewModel.left) {
            this.setLeftPane(viewModel.left);
        }
    }

    private async setRightPane(viewModel: RightPaneViewModel) {
        if (!this.right || this.right.kind !== viewModel.kind) {
            switch (viewModel.kind) {
                case 'SourceFile':
                    if (!await this.initializeEditorAsync()) {
                        // initialize sets the pane content so only set pane
                        // if editor was not initialized
                        this.setRightPaneChild(this.editorContainerPane);
                    }
                    break;
                case 'Overview':
                    this.loadOverview();
                    break;
            }
        }

        if (viewModel.kind == 'SourceFile') {
            this.applyFileViewModel(viewModel, <FileViewModel>{ kind: 'SourceFile' });
        }

        this.right = viewModel;
    }

    private async initializeEditorAsync(): Promise<boolean> {
        if (!this.editor) {
            this.editorContainerPane = document.createElement('div');
            this.editorContainerPane.style.height = 'calc(100% - 71px)';
            this.editorTemplate = document.getElementById('rightPaneEditorContents');
            let bottomPane = document.getElementById('bottomPane');
            this.editorBottomPaneTemplate = <HTMLElement>document.getElementById('bottomPane').cloneNode(true);
            bottomPane.innerHTML = '';
            this.editorTemplate.remove();
            this.editorContainerPane.innerHTML = this.editorTemplate.innerHTML;

            this.setRightPaneChild(this.editorContainerPane);

            // TODO: Maybe give editor pane a different id to start and then change to editor pane
            // here
            this.editorPane = document.getElementById('editorPane');
            this.editor = await CodexEditor.createAsync(this.server, this, this.editorPane);
            return true;
        }

        return false;
    }

    private replaceEditorDataTokens(sourceElement: HTMLElement, targetElement: HTMLElement) {
        let innerHtml = sourceElement.innerHTML;
        innerHtml = replaceAll(replaceAll(replaceAll(replaceAll(innerHtml,
            "{filePath}", this.editor.filePath),
            "{projectId}", this.editor.projectId),
            "{repoRelativePath}", this.editor.repoRelativePath),
            "{webLink}", this.editor.webLink);
        targetElement.innerHTML = innerHtml;
    }

    private populateEditorBottomPane() {
        let bottomPane = document.getElementById("bottomPane");
        this.replaceEditorDataTokens(this.editorBottomPaneTemplate, bottomPane);
        bottomPane.hidden = false;
    }

    private async applyFileViewModel(newModel: FileViewModel, oldModel: FileViewModel) {
        if (newModel.filePath !== oldModel.filePath || newModel.projectId !== oldModel.projectId) {
            const sourceFile = newModel.sourceFile || await this.server.getSourceFile(newModel.projectId, newModel.filePath);
            await this.editor.openFile(sourceFile, newModel.targetLocation);
            this.populateEditorBottomPane();
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
        return getUrlForState(newState)
    }

    async findAllReferences(projectId: string, symbol: string): Promise<void> {
        const html = await rpc.getFindAllReferencesHtml(projectId, symbol);
        updateReferences(html);
    }

    async showDocumentOutline(projectId: string, filePath: string) {
        notImplemented();
    }

    async showProjectExplorer(projectId: string) {
        notImplemented();
    }

    async showNamespaceExplorer(projectId: string) {
        notImplemented();
    }

    private loadRightPaneFrom(url: string) {
        rpc.server<string>(url).then(
            data => this.setRightPaneView(data),
            e => this.setRightPaneView("<div class='note'>" + e + "</div>"));
    }

    private loadLeftPaneFrom(url: string) {
        rpc.server<string>(url).then(
            data => setLeftPane(data),
            e => setLeftPane("<div class='note'>" + e + "</div>"));
    }

    private loadOverview() {
        this.loadRightPaneFrom("/overview/");
    }

    private setRightPaneView(text: string) {
        var rightPane = document.getElementById("rightPane");
        rightPane.innerHTML = text;
    }

    private setRightPaneChild(child: HTMLElement) {
        let rightPane = document.getElementById("rightPane");
        while (rightPane.firstChild) {
            rightPane.removeChild(rightPane.firstChild);
        }

        rightPane.appendChild(child);
    }
}