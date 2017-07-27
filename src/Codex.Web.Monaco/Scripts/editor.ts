/// <reference path="../node_modules/@types/jquery/index.d.ts"/>
/// <reference path="../node_modules/monaco-editor/monaco.d.ts"/>
/// <reference path="rpc.ts"/>
/// <reference path="scripts.ts"/>

// Required by monaco. Have no idea how to use it without it.
declare const require: any;

declare class SymbolicUri extends monaco.Uri {
    projectId: string;
    symbol: string;
} 

function registerProviders() {
    if (state.editorRegistered) {
        return;
    }

    state.editorRegistered = true;

    monaco.languages.register({ id: 'cdx' });

    monaco.languages.registerDocumentSymbolProvider('cdx', {
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

    monaco.languages.registerDefinitionProvider('cdx', {
        provideDefinition: function (model, position) {
            let offset = model.getOffsetAt(position);
            let reference = getReference(state.sourceFileModel, offset);
            let uri = <SymbolicUri>monaco.Uri.parse(`${encodeURI(reference.projectId)}/${encodeURI(reference.symbol)}`);
            uri.projectId = reference.projectId;
            uri.symbol = reference.symbol;

            return {
                uri: uri,
                range: { startLineNumber: 1, startColumn: 7, endLineNumber: 1, endColumn: 8 }
            }
        }
    });

    monaco.languages.registerReferenceProvider('cdx', {
        provideReferences: function (model, position) {
            var word = model.getWordAtPosition(position);
            if (word && word.word === "B") {
                return [{ uri: monaco.Uri.parse("bar/c"), range: { startLineNumber: 1, startColumn: 7, endLineNumber: 1, endColumn: 8 } }]
            }
            return [];
        }
    });

    
    monaco.languages.setTokensProvider('cdx', {
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
                                scopes: '',
                                startIndex: lastPosition - startPosition
                            };

                            tokenIndex++;
                        }

                        lastPosition = end;
                        if (lastPosition < endPosition) {
                            classificationIndex++;
                        }

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
    /*
    */



    //monaco.languages.register({ id: 'csharp' });
    //monaco.languages.registerDefinitionProvider('csharp', {
    //    provideDefinition: function (model, position) {
    //        var offset = model.getOffsetAt(position);
    //       var url = codexWebRootPrefix + "/definitionAtPosition/" + encodeURI(project) + "/?filename=" + encodeURIComponent(file) + "&position=" + encodeURIComponent(offset);
    //       return callServer(url, function (data) {
    //           var key = project + "/" + file;
    //           //var uri = monaco.Uri.parse(codexWebRootPrefix + data.url + "42");
    //           var uri = monaco.Uri.parse(key);
               
    //           return { uri: uri, range: { startLineNumber: 1, startColumn: 7, endLineNumber: 1, endColumn: 8 } };
    //       });
    //   } 
    //});

    //monaco.languages.registerReferenceProvider('csharp', {
    //    provideDefinition: function (model, position) {
    //        var offset = model.getOffsetAt(position);
    //        var url = codexWebRootPrefix + "/definitionAtPosition/" + encodeURI(project) + "/?filename=" + encodeURIComponent(file) + "&position=" + encodeURIComponent(offset);
    //        return callServer(url, function (data) {
    //            var key = project + "/" + file;
    //            //var uri = monaco.Uri.parse(codexWebRootPrefix + data.url + "42");
    //            var uri = monaco.Uri.parse(key);

    //            return { uri: uri, range: { startLineNumber: 1, startColumn: 7, endLineNumber: 1, endColumn: 8 } };
    //        });
    //    }
    //});
}

function getReferencesHtmlAtPosition(editor: monaco.editor.IEditor) : Promise<string> {
    let position = editor.getPosition();
    
    let offset = state.currentTextModel.getOffsetAt(position);

    let definition = getDefinition(state.sourceFileModel, offset) || getReference(state.sourceFileModel, offset);

    if (!definition) {
        return Promise.resolve(undefined);
    }

    return getFindAllReferencesHtml(definition.projectId, definition.symbol);
}

//function openEditor(input) {
//    alert(input.resource);
//    var uri = input.resource;
//    return callServer(uri, function(data) {
//        state.editor.setValue(data.contents);
//    });
//}

async function openEditor(input: { resource: SymbolicUri }) {
    let definitionLocation = await getDefinitionLocation(input.resource.projectId, input.resource.symbol);
    if (typeof definitionLocation === "string") {

    } else {
        var model = createModelFrom(
            definitionLocation.contents,
            definitionLocation.projectId,
            definitionLocation.filePath);
        state.sourceFileModel = definitionLocation;

        state.editor.setModel(model);

        // TODO: add try/catch, refactor the logic out
        state.editor.focus();
        if (definitionLocation.span) {
            var monacoPosition = state.currentTextModel.getPositionAt(definitionLocation.span.position);

            var position = { lineNumber: monacoPosition.lineNumber, column: monacoPosition.column };
            state.editor.revealPositionInCenter(position);
            state.editor.setPosition(position);
            state.editor.deltaDecorations([],
                [
                    {
                        range: new monaco.Range(position.lineNumber, 1, position.lineNumber, 1),
                        options: { className: 'highlightLine', isWholeLine: true }
                    }
                ]);
            state.editor.setSelection({ startLineNumber: position.lineNumber, startColumn: position.column, endLineNumber: position.lineNumber, endColumn: position.column + definitionLocation.span.length });
        }                    
    }

    return monaco.Promise.as(null);
}

var models;
//var models = {

//};

function createModelFrom(content: string, project: string, file: string) {
    if (state.currentTextModel) {
        state.currentTextModel.dispose();
    }

    var key = `${project}/${file}`;
    state.currentTextModel = monaco.editor.createModel(content, 'cdx', monaco.Uri.parse(key));
    return state.currentTextModel;
}

function createMonacoEditorAndDisplayFileContent(project: string, file: string, sourceFile: SourceFileContentsModel, lineNumber: number) {
    state.editor = undefined;
    state.sourceFileModel = sourceFile;
    if (!state.editor) {
        require.config({ paths: { 'vs': 'node_modules/monaco-editor/dev/vs' } });

        var editorPane = document.getElementById('editorPane');
        if (editorPane) {
            require(['vs/editor/editor.main'],
                function () {
                    class SymbolicUri extends monaco.Uri {
                        projectId: string;
                        symbol: string;
                    } 

                    registerProviders();

                    state.currentTextModel = createModelFrom(state.sourceFileModel.contents, project, file);

                    state.editor = monaco.editor.create(editorPane, {
                        // Don't need to specify a language, because model carries this information around.
                        model: state.currentTextModel,
                        readOnly: true,
                        theme: 'codex',
                        lineNumbers: "on",
                        scrollBeyondLastLine: true
                        }, {
                            editorService: { openEditor: openEditor },
                            
                            //textModelService: { createModelReference: createModelReference }
                        }
                    );

                    state.editor.addAction({
                        // An unique identifier of the contributed action.
                        id: 'Codex.FindAllReferences.LeftPane',

                        // A label of the action that will be presented to the user.
                        label: 'Find All References (Advanced)',

                        // An optional array of keybindings for the action.
                        keybindings: [monaco.KeyMod.CtrlCmd | monaco.KeyCode.F10],

                        // A precondition for this action.
                        precondition: null,

                        // A rule to evaluate on top of the precondition in order to dispatch the keybindings.
                        keybindingContext: null,

                        contextMenuGroupId: 'navigation',

                        contextMenuOrder: 1.5,

                        // Method that will be executed when the action is triggered.
                        // @param editor The editor instance is passed in as a convinience
                        run: function (ed) {
                            //let referencesHtml = getFindAllReferencesHtml()
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

                    state.editor.onMouseDown(function (e) {
                        contentNode.innerHTML = "Position: " + state.editor.getModel().getOffsetAt(e.target.position);
                    });
                    
                    state.editor.focus();
                    let position;
                    let length = 0;
                    if (sourceFile.span) {
                        position = state.currentTextModel.getPositionAt(sourceFile.span.position);
                        length = sourceFile.span.length;
                    } else if (lineNumber) {
                        position = { lineNumber: lineNumber, column: 1 }
                    }

                    if (position) {
                        state.editor.revealPositionInCenter(position);
                        state.editor.setPosition(position);
                        state.editor.deltaDecorations([],
                            [
                                {
                                    range: new monaco.Range(position.lineNumber, 1, position.lineNumber, 1),
                                    options: { className: 'highlightLine', isWholeLine: true }
                                }
                            ]);

                        state.editor.setSelection({
                            startLineNumber: position.lineNumber,
                            startColumn: position.column,
                            endLineNumber: position.lineNumber,
                            endColumn: position.column + length
                        });
                    }                    
            });
        }
    }
}