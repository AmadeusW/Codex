/// <reference path="types.ts" />
/// <reference path="editor.ts" />

class CodexEditor implements ICodexEditor {
    private sourceFileModel: SourceFile;
    private editor: monaco.editor.IStandaloneCodeEditor;
    private currentTextModel: monaco.editor.IModel;
    private models = {};

    private constructor(private webServer: ICodexWebServer, private webSite: ICodexWebPage, private container: HTMLElement) {

    }

    openFile(sourceFile: SourceFile, targetLocation?: TargetEditorLocation): Promise<void> | void {
        if (!(this.currentTextModel && sourceFile.filePath === this.sourceFileModel.filePath && sourceFile.projectId === this.sourceFileModel.projectId)) {
            this.currentTextModel = createModelFrom(sourceFile.contents, sourceFile.projectId, sourceFile.filePath);
            this.sourceFileModel = sourceFile;

            this.editor.setModel(this.currentTextModel);
        }

        if (targetLocation) {
            this.navigateTo(targetLocation);
        }
    }

    navigateTo(targetLocation: TargetEditorLocation) {
        this.editor.focus();
        let range = (
            () => {
                switch (targetLocation.kind) {
                    case "number": return this.getRangeAtOffset(targetLocation.value);
                    case "symbol": return this.getSymbolRange(targetLocation.value);
                    case "span": return this.getRangeAtSpan(targetLocation.value);

                }
            })();

        if (range) {
            const position = range.getStartPosition();
            this.editor.revealPositionInCenter(position);
            this.editor.setPosition(position);
            this.editor.deltaDecorations([],
                [
                    {
                        range: new monaco.Range(position.lineNumber, 1, position.lineNumber, 1),
                        options: { className: 'highlightLine', isWholeLine: true }
                    }
                ]);

            this.editor.setSelection({
                startLineNumber: position.lineNumber,
                startColumn: position.column,
                endLineNumber: position.lineNumber,
                endColumn: position.column + length
            });
        }
    }

    private getSymbolRange(symbol: Symbol): monaco.Range | undefined {
        if (!this.sourceFileModel.segments) {
            return undefined;
        }

        for (let segment of this.sourceFileModel.segments) {
            for (let symbolSpan of segment.definitions) {
                if (symbolSpan.symbol === symbol.symbolId) {
                    return this.getRangeAtSpan(symbolSpan.span);
                }
            }
        }

        return undefined;
    }


    private getRangeAtOffset(offset: number): monaco.Range {
        return this.getRangeAtSpan({ position: offset, length: 0 });
    }

    private getRangeAtSpan(span: Span): monaco.Range {
        const start = this.currentTextModel.getPositionAt(span.position);
        const end = this.currentTextModel.getPositionAt(span.position + span.length);

        let range = new monaco.Range(start.lineNumber, start.column, end.lineNumber, end.column);
        return range;
    }

    private getSymbolAtPosition(position?: monaco.IPosition): SymbolSpan {
        position = position || this.editor.getPosition();

        let offset = this.currentTextModel.getOffsetAt(position);

        return this.getDefinition(this.sourceFileModel, offset) || this.getReference(this.sourceFileModel, offset);
    }

    private getReferencesHtmlAtPosition(): Promise<string> {
        let definition = this.getSymbolAtPosition();
        if (!definition) {
            return Promise.resolve(undefined);
        }

        return getFindAllReferencesHtml(definition.projectId, definition.symbol);
    }

    private getReference(_this: SourceFile, position: number): SymbolSpan {
        let segmentIndex = ~~(position / _this.segmentLength);
        if (segmentIndex >= _this.segments.length) {
            return undefined;
        }

        let segment = _this.segments[segmentIndex];
        if (!segment) {
            return undefined;
        }

        for (let symbolSpan of segment.references) {
            if (position >= symbolSpan.span.position &&
                position <= (symbolSpan.span.position + symbolSpan.span.length)) {
                return symbolSpan;
            }
        }

        return undefined;
    }

    private getDefinition(_this: SourceFile, position: number): SymbolSpan {
        let segmentIndex = ~~(position / _this.segmentLength);
        if (segmentIndex >= _this.segments.length) {
            return undefined;
        }

        let segment = _this.segments[segmentIndex];
        if (!segment) {
            return undefined;
        }

        for (let symbolSpan of segment.definitions) {
            if (position >= symbolSpan.span.position &&
                position <= (symbolSpan.span.position + symbolSpan.span.length)) {
                return symbolSpan;
            }
        }

        return undefined;
    }

    public static createAsync(webServer: ICodexWebServer, webSite: ICodexWebPage, container: HTMLElement): Promise<CodexEditor> {
        const editor = new CodexEditor(webServer, webSite, container);
        return editor.createMonacoEditor().then(v => {
            editor.editor = v;
            return editor;
        });
    }

    private createMonacoEditor(): Promise<monaco.editor.IStandaloneCodeEditor> {
        console.log("createing a monaco editor");

        require.config({ paths: { 'vs': 'node_modules/monaco-editor/dev/vs' } });

        return new Promise((resolve, reject) => {
            require(['vs/editor/editor.main'],
                () => {
                    console.log("require callback")
                    // Need to define a class that extends the monaco.Uri
                    // only when the monaco stuff is loaded.
                    class SymbolicUri extends monaco.Uri {
                        projectId: string;
                        symbol: string;
                    }

                    this.registerEditorProviders();
                    const editor = monaco.editor.create(this.container, {
                            // Don't need to specify a language, because model carries this information around.
                            readOnly: true,
                            theme: 'codex',
                            lineNumbers: lineNumberProvider,
                            scrollBeyondLastLine: true
                        },
                        {
                            editorService: { openEditor: input => this.openEditor(input) },
                            textModelService: { createModelReference: input => this.createModelReference(input) }
                        });

                    // Ctrl + click goes to the symbol or to the references of the symbol
                    this.registerCtrlClickBehaviour(editor);
                    this.registerEditorActions(editor);
                    this.registerFocusSearchBox(editor);
                    this.addToolbarWidget(editor);

                    // For debugging purposes only.
                    this.debugDisplayPosition(editor);

                    resolve(editor);
                    return editor;
                });
        });
    }

    private createModelFrom(content: string, project: string, file: string) {
        if (this.currentTextModel) {
            this.currentTextModel.dispose();

            for (let modelId in this.models) {
                this.models[modelId].dispose();
            }

            this.models = {};
        }
        
        this.currentTextModel = this.getOrCreateModelFrom(content, project, file);
        return this.currentTextModel;
    }

    private getOrCreateModelFrom(content: string, project: string, file: string, language: string = undefined) {
        let key = `${project}/${file}/`;

        let model = this.models[key];
        if (!model) {
            let uri = monaco.Uri.parse(key);
            model = monaco.editor.createModel(content, language || codexLanguage, uri);
            this.models[key] = model;
        }

        return model;
    }

    private async openEditor(input: { resource: SymbolicUri }) {
        let source = input.resource.definitionResult;
        if (source) {
            if (typeof source === "string") {
                await updateReferences(source);
            } else {
                this.openFile(source, {kind: 'span', value: source.span});
            }
        }

        return monaco.Promise.as(null);
    }

    private createModelReference(input: SymbolicUri) {
        let d = input.definitionResult;
        if (typeof d === "string") {
            return monaco.Promise.as(null);
        } else {
            const model = getOrCreateModelFrom(d.contents, d.projectId, d.filePath, 'csharp');

            return monaco.Promise.as({ object: { textEditorModel: model, dispose: () => {} }, dispose: () => {} });
        }
    }

    private registerEditorProviders() {
        console.log("registering editor providers");

        monaco.languages.register({ id: codexLanguage });
        monaco.languages.registerDocumentSymbolProvider(codexLanguage, {
            provideDocumentSymbols: (model) => {
                let result = this.sourceFileModel.documentSymbols.map(d => {

                    return {
                        name: d.name,
                        containerName: d.containerName,
                        kind: monaco.languages.SymbolKind[d.symbolKind],
                        location: {
                            uri: undefined,
                            range: this.getRangeAtSpan(d.span)
                        }
                    }
                });

                return result;
            }
        });

        monaco.languages.registerImplementationProvider(codexLanguage, {
            provideImplementation: (model, position) => {
                var reference = this.getSymbolAtPosition(position);
                if (!reference) {
                    return undefined;
                }

                let uri = <SymbolicUri>monaco.Uri.parse(`${encodeURI(reference.projectId)}/${encodeURI(reference.symbol)}`);
                uri.projectId = reference.projectId;
                uri.symbol = reference.symbol;

                let uri2 = <SymbolicUri>monaco.Uri.parse(`${encodeURI(reference.projectId)}/${encodeURI(reference.symbol)}/2`);
                uri2.projectId = reference.projectId;
                uri2.symbol = reference.symbol;

                return [
                    {
                        uri: uri,
                        range: { startLineNumber: 1, startColumn: 7, endLineNumber: 1, endColumn: 8 }
                    }, {
                        uri: uri2,
                        range: { startLineNumber: 5, startColumn: 7, endLineNumber: 5, endColumn: 8 }
                    }
                ];
            }
        });

        monaco.languages.registerDefinitionProvider(codexLanguage, {
            provideDefinition: async (model, position) => {
                let offset = model.getOffsetAt(position);
                let reference = this.getReference(this.sourceFileModel, offset);
                if (!reference) {
                    return undefined;
                }

                let definitionResult = await getDefinitionLocation(reference.projectId, reference.symbol);

                // URI is a bit weird in monaco
                // It strips out /? part of the uri.
                // So we're not relying on the real uri here and using
                // uri just as a key that is required by the monaco.
                // Real data is passed using the fields in the derived type - SymbolicUri
                let uri = <SymbolicUri>monaco.Uri.parse(`${encodeURI(reference.projectId)}/${encodeURI(reference.symbol)}`);
                uri.projectId = reference.projectId;
                uri.symbol = reference.symbol;
                uri.definitionResult = definitionResult;

                let range: monaco.IRange = { startLineNumber: 1, startColumn: 1, endLineNumber: 1, endColumn: 1 };

                if (!(typeof definitionResult === "string")) {
                    range = {
                        startLineNumber: definitionResult.span.line,
                        startColumn: definitionResult.span.column,
                        endLineNumber: definitionResult.span.line,
                        endColumn: definitionResult.span.column + definitionResult.span.length
                    };
                }

                return {
                    uri: uri,
                    range
                }
            }
        });

        monaco.languages.registerHoverProvider(codexLanguage, {
            provideHover: (model, position) => {
                let reference = this.getSymbolAtPosition(position);
                if (!reference) {
                    return undefined;
                }
                let uri = <SymbolicUri>monaco.Uri.parse(`${encodeURI(reference.projectId)}/${encodeURI(reference.symbol)}`);
                uri.projectId = reference.projectId;
                uri.symbol = reference.symbol;

                return getToolTip(reference.projectId, reference.symbol)
                    .then(res => {
                        if (!res || !res.projectId) {
                            return undefined;
                        }

                        return {
                            range: this.getRangeAtSpan(reference.span),
                            contents: [
                                generateToolTipHeader(res),
                                ...generateToolTipBody(res),
                                //{ language: 'markdown', value: generateToolTipBody(res) }
                            ]
                        }
                    });
            }
        });

        /**
         * Extra state required by the tokenizer.
         */
        class CustomState implements monaco.languages.IState {
            line: number;
            classificationIndex: number;

            constructor(line: number, classificationIndex: number) {
                this.line = line;
                this.classificationIndex = classificationIndex;
            }

            clone(): monaco.languages.IState {
                return new CustomState(this.line, this.classificationIndex);
            }

            equals(other: monaco.languages.IState): boolean {
                let otherState = <CustomState>other;
                return otherState.line === this.line && otherState.classificationIndex === this.classificationIndex;
            }
        }

        // monaco has built-in facilities for colorizatio,
        // but codex has better information and can colorize some stuff
        // that can't be colorized on the syntax level (like when some dotted identifier
        // uses a type).
        monaco.languages.setTokensProvider(codexLanguage, {
            getInitialState: () => new CustomState(1, 0),
            tokenize: (line, tokenizerState: CustomState) => {
                let tokens: Array<monaco.languages.IToken> = [];

                let startPosition = this.currentTextModel.getOffsetAt({ lineNumber: tokenizerState.line, column: 1 });
                let endPosition = this.currentTextModel.getOffsetAt({
                    lineNumber: tokenizerState.line,
                    column: this.currentTextModel.getLineMaxColumn(tokenizerState.line)
                });

                let classifications = this.sourceFileModel.classifications;
                let classificationIndex = tokenizerState.classificationIndex;

                if (classifications) {
                    let tokenIndex = 0;
                    let lastPosition = startPosition;
                    for (let i = tokenizerState.classificationIndex; i < classifications.length; i++) {
                        let classification = classifications[i];
                        let start = Math.max(startPosition, classification.position);
                        let end = Math.min(classification.position + classification.length, endPosition);

                        if (end < startPosition) {
                            classificationIndex++;
                        } else if (classification.position <= endPosition) {
                            if (lastPosition < start) {
                                tokens[tokenIndex] = {
                                    // Tokens only specify start index so a token needs to be added
                                    // to indicate end index / span between colorized spans. scopes = '' means no colorization
                                    scopes: '',
                                    startIndex: lastPosition - startPosition
                                };

                                tokenIndex++;
                            }

                            lastPosition = end;
                            if (lastPosition < endPosition) {
                                classificationIndex++;
                            }

                            // Add actual token for colorized span. NOTE: This is only the start. The end
                            // is specified later when we encounter the next colorized span or end of line
                            tokens[tokenIndex] = {
                                scopes: 'cdx.' + classification.name,
                                startIndex: start - startPosition
                            };
                            tokenIndex++;
                        } else {
                            break;
                        }
                    }

                    if (lastPosition < endPosition) {
                        // Tokens only specify start index so a token needs to be added
                        // to indicate end index / span between colorized spans. scopes = '' means no colorization
                        tokens[tokenIndex] = {
                            scopes: '',
                            startIndex: lastPosition - startPosition
                        };
                    }
                }

                return {
                    tokens: tokens,
                    endState: new CustomState(tokenizerState.line + 1, classificationIndex)
                };
            }
        });

        monaco.editor.defineTheme('codex', {
            base: 'vs',
            inherit: true,
            rules: [
                // keyword
                { token: 'cdx.k', foreground: '0000ff' },
                // type
                { token: 'cdx.t', foreground: '2b91af' },
                // comment
                { token: 'cdx.c', foreground: '008000' },
                // string literal
                { token: 'cdx.s', foreground: 'a31515' },
                // xml delimiter
                { token: 'cdx.xd', foreground: '0000ff' },
                // xml name
                { token: 'cdx.xn', foreground: 'A31515' },
                // xml name
                { token: 'cdx.xn', foreground: 'A31515' },
                // xml attribute name
                { token: 'cdx.xan', foreground: 'ff0000' },
                // xml attribute value
                { token: 'cdx.xav', foreground: '0000ff' },
                // xml entity reference
                { token: 'cdx.xer', foreground: 'ff0000' },
                // xml CDATA section
                { token: 'cdx.xcs', foreground: '808080' },
                // xml literal delimeter
                { token: 'cdx.xld', foreground: '6464B9' },
                // xml processing instruction
                { token: 'cdx.xpi', foreground: '808080' },
                // xml literal name
                { token: 'cdx.xln', foreground: '844646' },
                // xml literal attribute name
                { token: 'cdx.xlan', foreground: 'B96464' },
                // xml literal attribute value
                { token: 'cdx.xlav', foreground: '6464B9' },
                // xml literal attribute quotes
                { token: 'cdx.xlaq', foreground: '555555' },
                // xml literal CDATA section
                { token: 'cdx.xlcs', foreground: 'C0C0C0' },
                // xml literal entity reference
                { token: 'cdx.xler', foreground: 'B96464' },
                // xml literal embedded expression name
                { token: 'cdx.xlee', foreground: 'B96464', background: 'FFFEBF' },
                // xml literal processing instruction
                { token: 'cdx.xlpi', foreground: 'C0C0C0' },
                // excluded code
                { token: 'cdx.e', foreground: '808080' }
            ],
            colors: {}
        });
    }

    private debugDisplayPosition(editor: monaco.editor.IStandaloneCodeEditor) {
        let contentNode = document.createElement('div');
        contentNode.innerHTML = 'My content widget';
        contentNode.style.background = 'grey';
        contentNode.style.top = '50px';
        var contentWidget: monaco.editor.IOverlayWidget = {
            getId: () => 'my.content.widget',
            getDomNode: () => contentNode,
            getPosition: () => null
        };

        // TODO: Add official current line info widget
        //state.editor.addOverlayWidget(contentWidget);

        editor.onMouseDown((e) => {
            if (e.target.position) {
                contentNode.innerHTML = "Position: " + editor.getModel().getOffsetAt(e.target.position);
            }
        });
    }


    private addToolbarWidget(editor: monaco.editor.IStandaloneCodeEditor) {
        var toolBarPane = document.createElement('div');
        var documentOutlineButton = document.createElement('img');
        documentOutlineButton.setAttribute('src', '../../content/icons/DocumentOutline.png');
        documentOutlineButton.title = "Document Outline";
        documentOutlineButton.className = 'documentOutlineButton';
        documentOutlineButton.onclick = showDocumentOutline;
        toolBarPane.appendChild(documentOutlineButton);

        var projectExplorerButton = document.createElement('img');
        var projectExplorerIcon = '../../content/icons/CSharpProjectExplorer.png';

        projectExplorerButton.setAttribute('src', projectExplorerIcon);
        projectExplorerButton.title = "Project Explorer";
        projectExplorerButton.className = 'projectExplorerButton';
        projectExplorerButton.onclick = () => { document.getElementById('projectExplorerLink').click(); };
        toolBarPane.appendChild(projectExplorerButton);

        var namespaceExplorerButton = document.createElement('img');
        namespaceExplorerButton.setAttribute('src', '../../content/icons/NamespaceExplorer.png');
        namespaceExplorerButton.title = "Namespace Explorer";
        namespaceExplorerButton.className = 'namespaceExplorerButton';
        namespaceExplorerButton.onclick = showNamespaceExplorer;

        var toolBarWidget: monaco.editor.IOverlayWidget = {
            getId: () => 'codex.toolbar.widget',
            getDomNode: () => toolBarPane,
            getPosition: () => {
                return { preference: monaco.editor.OverlayWidgetPositionPreference.TOP_RIGHT_CORNER };
            }
        };

        editor.addOverlayWidget(toolBarWidget);
    }

    private registerEditorActions(editor: monaco.editor.IStandaloneCodeEditor) {
        let actions = editor.getSupportedActions();
        editor.addAction({
            // An unique identifier of the contributed action.
            id: 'Codex.FindAllReferences.LeftPane',

            // A label of the action that will be presented to the user.
            label: 'Find All References',

            // An optional array of keybindings for the action.
            keybindings: [monaco.KeyMod.Shift | monaco.KeyCode.F12],

            // A precondition for this action.
            precondition: null,

            // A rule to evaluate on top of the precondition in order to dispatch the keybindings.
            keybindingContext: null,

            contextMenuGroupId: 'navigation',

            contextMenuOrder: 1.5,

            // Method that will be executed when the action is triggered.
            // @param editor The editor instance is passed in as a convinience
            run: () => {
                let referencesHtml = this.getReferencesHtmlAtPosition()
                    .then(html => {
                        if (html) {
                            updateReferences(html);
                        }
                    },
                    e => { }
                    );
            }
        });
    }

    private registerFocusSearchBox(editor: monaco.editor.IStandaloneCodeEditor) {
        editor.addAction({
            // An unique identifier of the contributed action.
            id: 'Codex.SearchBox.SetFocus',

            // A label of the action that will be presented to the user.
            label: 'Set focus on the search box',

            // An optional array of keybindings for the action.
            keybindings: [monaco.KeyMod.CtrlCmd | monaco.KeyCode.US_COMMA],

            // A precondition for this action.
            precondition: null,

            // A rule to evaluate on top of the precondition in order to dispatch the keybindings.
            keybindingContext: null,

            // Method that will be executed when the action is triggered.
            // @param editor The editor instance is passed in as a convinience
            run: (ed) => {
                state.searchBox.focus();
            }
        });
    }

    private registerCtrlClickBehaviour(editor: monaco.editor.IStandaloneCodeEditor) {
        editor.onMouseDown(e => {
            if (e.event.ctrlKey) {
                var target = getTargetAtPosition(editor).then(definitionLocation => {
                    openEditorForLocation(definitionLocation);
                },
                    e => { });
            }
        });

        editor.onMouseMove(e => {
            if (e.event.ctrlKey) {
                let symbol = getSymbolAtPosition(editor, e.target.position);
                if (symbol) {
                    let start = editor.getModel().getPositionAt(symbol.span.position);
                    let end = editor.getModel().getPositionAt(symbol.span.position + symbol.span.length);

                    state.ctrlClickLinkDecorations = editor.deltaDecorations(state.ctrlClickLinkDecorations || [], [
                        {
                            range: new monaco.Range(start.lineNumber, start.column, end.lineNumber, end.column),
                            options: { inlineClassName: 'blueLink' }
                        }]);
                    return;
                }
            }

            if (state.ctrlClickLinkDecorations) {
                state.ctrlClickLinkDecorations = editor.deltaDecorations(state.ctrlClickLinkDecorations || [], []);
            }
        });

    }
}