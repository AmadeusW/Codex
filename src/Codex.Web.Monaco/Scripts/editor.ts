/// <reference path="../node_modules/@types/jquery/index.d.ts"/>
/// <reference path="../node_modules/monaco-editor/monaco.d.ts"/>
/// <reference path="rpc.ts"/>
/// <reference path="scripts.ts"/>

// Required by monaco. Have no idea how to use it without it.
declare const require: any;

// This is weird, but we can only declare type here, but can't define it.
// The monaco itself could be not loaded here yet.
declare class SymbolicUri extends monaco.Uri {
    projectId: string;
    symbol: string;
    definitionResult: SourceFileOrView;
    model: monaco.editor.IModel;
}

const codexLanguage = 'cdx';

function createModelFrom(content: string, project: string, file: string) {
    if (state.currentTextModel) {
        state.currentTextModel.dispose();

        for (let modelId in models) {
            models[modelId].dispose();
        }

        models = {};
    }
    
    state.currentTextModel = getOrCreateModelFrom(content, project, file);
    return state.currentTextModel;
}

let models = {};

function getOrCreateModelFrom(content: string, project: string, file: string, language: string = undefined) {
    let key = `${project}/${file}/`;

    let model = models[key];
    if (!model) {
        let uri = monaco.Uri.parse(key);
        model = monaco.editor.createModel(content, language || codexLanguage, uri);
        models[key] = model;
    }

    return model;
}

function createMonacoEditorAndDisplayFileContent(project: string, file: string, sourceFile: SourceFileContentsModel, lineNumber: number) {
    // Need to initialize the edit each time now.
    //state.editor = undefined;
    state.sourceFileModel = sourceFile;

    if (!state.editor) {
        require.config({ paths: { 'vs': 'node_modules/monaco-editor/dev/vs' } });

        var editorPane = document.getElementById('editorPane');
        if (editorPane) {
            require(['vs/editor/editor.main'],
                function () {
                    // Need to define a class that extends the monaco.Uri
                    // only when the monaco stuff is loaded.
                    class SymbolicUri extends monaco.Uri {
                        projectId: string;
                        symbol: string;
                    }

                    registerEditorProviders();

                    state.currentTextModel = createModelFrom(state.sourceFileModel.contents, project, file);

                    state.editor = monaco.editor.create(editorPane, {
                        // Don't need to specify a language, because model carries this information around.
                        model: state.currentTextModel,
                        readOnly: true,
                        theme: 'codex',
                        lineNumbers: lineNumberProvider,
                        scrollBeyondLastLine: true
                    },
                        {
                            editorService: { openEditor: openEditor },
                            textModelService: {createModelReference: createModelReference }
                        });

                    // Ctrl + click goes to the symbol or to the references of the symbol
                    registerCtrlClickBehaviour(state.editor);
                    registerEditorActions(state.editor);
                    registerFocusSearchBox(state.editor);
                    addToolbarWidget(state.editor);

                    // For debugging purposes only.
                    debugDisplayPosition(state.editor);

                    changeEditorPositionTo(state.editor, sourceFile.span, lineNumber);
                });
        }
    }
    else {
        // Not working yet. Need to fix state management first.
        // Editor is already existed.
        state.currentTextModel = createModelFrom(state.sourceFileModel.contents, project, file);
        state.editor.setModel(state.currentTextModel);
        changeEditorPositionTo(state.editor, sourceFile.span, lineNumber);

    }
}

// Transforms lineNumber into a hyperlink
function lineNumberProvider(lineNumber: number) {
    var url = getUrlForState(state.currentState) + "&line=" + lineNumber;
    return "<a href='" + url + "'>" + lineNumber + "</a>";
}

function changeEditorPositionTo(editor: monaco.editor.IStandaloneCodeEditor, span: Span, lineNumber: number) {
    editor.focus();
    let position;
    let length = 0;
    if (span) {
        position = state.currentTextModel.getPositionAt(span.position);
        length = span.length;
    } else if (lineNumber) {
        position = { lineNumber: lineNumber, column: 1 }
    }

    if (position) {
        editor.revealPositionInCenter(position);
        editor.setPosition(position);
        editor.deltaDecorations([],
            [
                {
                    range: new monaco.Range(position.lineNumber, 1, position.lineNumber, 1),
                    options: { className: 'highlightLine', isWholeLine: true }
                }
            ]);

        editor.setSelection({
            startLineNumber: position.lineNumber,
            startColumn: position.column,
            endLineNumber: position.lineNumber,
            endColumn: position.column + length
        });
    }
}

function getSymbolAtPosition(editor: monaco.editor.IEditor, position?: monaco.IPosition): SymbolSpan {
    position = position || editor.getPosition();

    let offset = state.currentTextModel.getOffsetAt(position);

    return getDefinition(state.sourceFileModel, offset) || getReference(state.sourceFileModel, offset);
}

function getReferencesHtmlAtPosition(editor: monaco.editor.IEditor): Promise<string> {
    let definition = getSymbolAtPosition(editor);
    if (!definition) {
        return Promise.resolve(undefined);
    }

    return getFindAllReferencesHtml(definition.projectId, definition.symbol);
}

function getTargetAtPosition(editor: monaco.editor.IEditor): Promise<SourceFileOrView> {
    let position = editor.getPosition();

    let offset = state.currentTextModel.getOffsetAt(position);

    let definition = getDefinition(state.sourceFileModel, offset);

    if (definition) {
        return getFindAllReferencesHtml(definition.projectId, definition.symbol);
    }

    let reference = getReference(state.sourceFileModel, offset);
    if (reference) {
        return getDefinitionLocation(reference.projectId, reference.symbol);
    }

    return Promise.resolve(undefined);
}

function openEditor(input: { resource: SymbolicUri }) {
    let definitionLocation = input.resource.definitionResult;
    return openEditorForLocation(definitionLocation);
}


function createModelReference(input: SymbolicUri) {
    let d = input.definitionResult;
    if (typeof d === "string") {
        return monaco.Promise.as(null);
    } else {
        const model = getOrCreateModelFrom(d.contents, d.projectId, d.filePath, 'csharp');

        return monaco.Promise.as({ object: { textEditorModel: model, dispose: () => {} }, dispose: () => {} });
    }
}

async function openEditorForLocation(source: SourceFileOrView) {
    if (typeof source === "string") {
        await updateReferences(source);
    } else {
        var model = createModelFrom(
            source.contents,
            source.projectId,
            source.filePath);
        state.sourceFileModel = source;

        state.editor.setModel(model);

        changeEditorPositionTo(state.editor, source.span, undefined);
    }

    return monaco.Promise.as(null);
}

function registerEditorProviders() {
    if (state.editorRegistered) {
        return;
    }

    state.editorRegistered = true;

    monaco.languages.register({ id: codexLanguage });

    monaco.languages.registerDocumentSymbolProvider(codexLanguage, {
        provideDocumentSymbols: function (model) {
            let result = state.sourceFileModel.documentSymbols.map(d => {

                let location1 = model.getPositionAt(d.span.position);
                let location2 = model.getPositionAt(d.span.position + d.span.length);

                return {
                    name: d.name,
                    containerName: d.containerName,
                    kind: monaco.languages.SymbolKind[d.symbolKind],
                    location: {
                        uri: undefined,
                        range: { startLineNumber: location1.lineNumber, startColumn: location1.column, endLineNumber: location2.lineNumber, endColumn: location2.column }
                    }
                }
            });

            return result;
        }
    });

    monaco.languages.registerImplementationProvider(codexLanguage, {
        provideImplementation: function (model, position) {
            var reference = getSymbolAtPosition(state.editor, position);
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
        provideDefinition: async function (model, position) {
            let offset = model.getOffsetAt(position);
            let reference = getReference(state.sourceFileModel, offset);
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

    //function getRangeFromSpan(span: Span): monaco.IRange {
        
    //}

    //monaco.languages.registerReferenceProvider(codexLanguage, {
    //    provideReferences: function (model, position) {
    //        // To be implemented.
    //        //var word = model.getWordAtPosition(position);
    //        //if (word && word.word === "B") {
    //        //    return [{ uri: monaco.Uri.parse("bar/c"), range: { startLineNumber: 1, startColumn: 7, endLineNumber: 1, endColumn: 8 } }]
    //        //}
    //        return [];
    //    }
    //});

    monaco.languages.registerHoverProvider(codexLanguage, {
        provideHover: function (model, position) {
            let reference = getSymbolAtPosition(state.editor, position);
            if (!reference) {
                return undefined;
            }
            let uri = <SymbolicUri>monaco.Uri.parse(`${encodeURI(reference.projectId)}/${encodeURI(reference.symbol)}`);
            uri.projectId = reference.projectId;
            uri.symbol = reference.symbol;
            const span = reference.span;

            const left = model.getPositionAt(span.position);
            const right = model.getPositionAt(span.position + span.length);

            return getToolTip(reference.projectId, reference.symbol)
                .then(function (res) {
                    if (!res || !res.projectId) {
                        return undefined;
                    }

                    return {
                        range: new monaco.Range(left.lineNumber, left.column, right.lineNumber, right.column),
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

            let startPosition = state.currentTextModel.getOffsetAt({ lineNumber: tokenizerState.line, column: 1 });
            let endPosition = state.currentTextModel.getOffsetAt({
                lineNumber: tokenizerState.line,
                column: state.currentTextModel.getLineMaxColumn(tokenizerState.line)
            });

            let classifications = state.sourceFileModel.classifications;
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

function debugDisplayPosition(editor: monaco.editor.IStandaloneCodeEditor) {
    let contentNode = document.createElement('div');
    contentNode.innerHTML = 'My content widget';
    contentNode.style.background = 'grey';
    contentNode.style.top = '50px';
    var contentWidget: monaco.editor.IOverlayWidget = {
        getId: function () {
            return 'my.content.widget';
        },
        getDomNode: function () {
            return contentNode;
        },
        getPosition: function () {
            return null;
        }
    };

    // TODO: Add official current line info widget
    //state.editor.addOverlayWidget(contentWidget);

    editor.onMouseDown(function (e) {
        if (e.target.position) {
            contentNode.innerHTML = "Position: " + state.editor.getModel().getOffsetAt(e.target.position);
        }
    });
}

function addToolbarWidget(editor: monaco.editor.IStandaloneCodeEditor) {
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
    projectExplorerButton.onclick = function () { document.getElementById('projectExplorerLink').click(); };
    toolBarPane.appendChild(projectExplorerButton);

    var namespaceExplorerButton = document.createElement('img');
    namespaceExplorerButton.setAttribute('src', '../../content/icons/NamespaceExplorer.png');
    namespaceExplorerButton.title = "Namespace Explorer";
    namespaceExplorerButton.className = 'namespaceExplorerButton';
    namespaceExplorerButton.onclick = showNamespaceExplorer;

    var toolBarWidget: monaco.editor.IOverlayWidget = {
        getId: function () {
            return 'codex.toolbar.widget';
        },
        getDomNode: function () {
            return toolBarPane;
        },
        getPosition: function () {
            return { preference: monaco.editor.OverlayWidgetPositionPreference.TOP_RIGHT_CORNER };
        }
    };

    editor.addOverlayWidget(toolBarWidget);
}

function registerEditorActions(editor: monaco.editor.IStandaloneCodeEditor) {
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
        run: function (ed) {
            let referencesHtml = getReferencesHtmlAtPosition(ed)
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

function registerFocusSearchBox(editor: monaco.editor.IStandaloneCodeEditor) {
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
        run: function (ed) {
            state.searchBox.focus();
        }
    });
}

function registerCtrlClickBehaviour(editor: monaco.editor.IStandaloneCodeEditor) {
    editor.onMouseDown(e => {
        if (e.event.ctrlKey) {
            var target = getTargetAtPosition(state.editor).then(definitionLocation => {
                openEditorForLocation(definitionLocation);
            },
                e => { });
        }
    });

    editor.onMouseMove(e => {
        if (e.event.ctrlKey) {
            let symbol = getSymbolAtPosition(state.editor, e.target.position);
            if (symbol) {
                let start = state.editor.getModel().getPositionAt(symbol.span.position);
                let end = state.editor.getModel().getPositionAt(symbol.span.position + symbol.span.length);

                state.ctrlClickLinkDecorations = state.editor.deltaDecorations(state.ctrlClickLinkDecorations || [], [
                    {
                        range: new monaco.Range(start.lineNumber, start.column, end.lineNumber, end.column),
                        options: { inlineClassName: 'blueLink' }
                    }]);
                return;
            }
        }

        if (state.ctrlClickLinkDecorations) {
            state.ctrlClickLinkDecorations = state.editor.deltaDecorations(state.ctrlClickLinkDecorations || [], []);
        }
    });

}