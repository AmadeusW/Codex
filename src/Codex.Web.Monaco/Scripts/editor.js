/// <reference path="../node_modules/@types/jquery/index.d.ts"/>
/// <reference path="../node_modules/monaco-editor/monaco.d.ts"/>
/// <reference path="rpc.ts"/>
/// <reference path="scripts.ts"/>
var __extends = (this && this.__extends) || (function () {
    var extendStatics = Object.setPrototypeOf ||
        ({ __proto__: [] } instanceof Array && function (d, b) { d.__proto__ = b; }) ||
        function (d, b) { for (var p in b) if (b.hasOwnProperty(p)) d[p] = b[p]; };
    return function (d, b) {
        extendStatics(d, b);
        function __() { this.constructor = d; }
        d.prototype = b === null ? Object.create(b) : (__.prototype = b.prototype, new __());
    };
})();
var __awaiter = (this && this.__awaiter) || function (thisArg, _arguments, P, generator) {
    return new (P || (P = Promise))(function (resolve, reject) {
        function fulfilled(value) { try { step(generator.next(value)); } catch (e) { reject(e); } }
        function rejected(value) { try { step(generator["throw"](value)); } catch (e) { reject(e); } }
        function step(result) { result.done ? resolve(result.value) : new P(function (resolve) { resolve(result.value); }).then(fulfilled, rejected); }
        step((generator = generator.apply(thisArg, _arguments || [])).next());
    });
};
var __generator = (this && this.__generator) || function (thisArg, body) {
    var _ = { label: 0, sent: function() { if (t[0] & 1) throw t[1]; return t[1]; }, trys: [], ops: [] }, f, y, t, g;
    return g = { next: verb(0), "throw": verb(1), "return": verb(2) }, typeof Symbol === "function" && (g[Symbol.iterator] = function() { return this; }), g;
    function verb(n) { return function (v) { return step([n, v]); }; }
    function step(op) {
        if (f) throw new TypeError("Generator is already executing.");
        while (_) try {
            if (f = 1, y && (t = y[op[0] & 2 ? "return" : op[0] ? "throw" : "next"]) && !(t = t.call(y, op[1])).done) return t;
            if (y = 0, t) op = [0, t.value];
            switch (op[0]) {
                case 0: case 1: t = op; break;
                case 4: _.label++; return { value: op[1], done: false };
                case 5: _.label++; y = op[1]; op = [0]; continue;
                case 7: op = _.ops.pop(); _.trys.pop(); continue;
                default:
                    if (!(t = _.trys, t = t.length > 0 && t[t.length - 1]) && (op[0] === 6 || op[0] === 2)) { _ = 0; continue; }
                    if (op[0] === 3 && (!t || (op[1] > t[0] && op[1] < t[3]))) { _.label = op[1]; break; }
                    if (op[0] === 6 && _.label < t[1]) { _.label = t[1]; t = op; break; }
                    if (t && _.label < t[2]) { _.label = t[2]; _.ops.push(op); break; }
                    if (t[2]) _.ops.pop();
                    _.trys.pop(); continue;
            }
            op = body.call(thisArg, _);
        } catch (e) { op = [6, e]; y = 0; } finally { f = t = 0; }
        if (op[0] & 5) throw op[1]; return { value: op[0] ? op[1] : void 0, done: true };
    }
};
function registerProviders() {
    if (state.editorRegistered) {
        return;
    }
    state.editorRegistered = true;
    monaco.languages.register({ id: 'cdx' });
    monaco.languages.registerDocumentSymbolProvider('cdx', {
        provideDocumentSymbols: function (model) {
            var result = state.sourceFileModel.documentSymbols.map(function (d) {
                var location1 = model.getPositionAt(d.span.position);
                var location2 = model.getPositionAt(d.span.position + d.span.length);
                return {
                    name: d.name,
                    containerName: d.containerName,
                    kind: monaco.languages.SymbolKind[d.symbolKind],
                    location: {
                        uri: undefined,
                        range: { startLineNumber: location1.lineNumber, startColumn: location1.column, endLineNumber: location2.lineNumber, endColumn: location2.column }
                    }
                };
            });
            return result;
        }
    });
    var CustomState = (function () {
        function CustomState(line, classificationIndex) {
            this.line = line;
            this.classificationIndex = classificationIndex;
        }
        CustomState.prototype.clone = function () {
            return new CustomState(this.line, this.classificationIndex);
        };
        CustomState.prototype.equals = function (other) {
            var otherState = other;
            return otherState.line === this.line && otherState.classificationIndex === this.classificationIndex;
        };
        return CustomState;
    }());
    monaco.languages.registerDefinitionProvider('cdx', {
        provideDefinition: function (model, position) {
            var offset = model.getOffsetAt(position);
            var reference = getReference(state.sourceFileModel, offset);
            var uri = monaco.Uri.parse(encodeURI(reference.projectId) + "/" + encodeURI(reference.symbol));
            uri.projectId = reference.projectId;
            uri.symbol = reference.symbol;
            return {
                uri: uri,
                range: { startLineNumber: 1, startColumn: 7, endLineNumber: 1, endColumn: 8 }
            };
        }
    });
    monaco.languages.registerReferenceProvider('cdx', {
        provideReferences: function (model, position) {
            var word = model.getWordAtPosition(position);
            if (word && word.word === "B") {
                return [{ uri: monaco.Uri.parse("bar/c"), range: { startLineNumber: 1, startColumn: 7, endLineNumber: 1, endColumn: 8 } }];
            }
            return [];
        }
    });
    monaco.languages.setTokensProvider('cdx', {
        getInitialState: function () { return new CustomState(1, 0); },
        tokenize: function (line, tokenizerState) {
            var tokens = [];
            var startPosition = state.currentTextModel.getOffsetAt({ lineNumber: tokenizerState.line, column: 1 });
            var endPosition = state.currentTextModel.getOffsetAt({
                lineNumber: tokenizerState.line,
                column: state.currentTextModel.getLineMaxColumn(tokenizerState.line)
            });
            var classifications = state.sourceFileModel.classifications;
            var classificationIndex = tokenizerState.classificationIndex;
            if (classifications) {
                var tokenIndex = 0;
                var lastPosition = startPosition;
                for (var i = tokenizerState.classificationIndex; i < classifications.length; i++) {
                    var classification = classifications[i];
                    var start = Math.max(startPosition, classification.position);
                    var end = Math.min(classification.position + classification.length, endPosition);
                    if (end < startPosition) {
                        classificationIndex++;
                    }
                    else if (classification.position <= endPosition) {
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
                    }
                    else {
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
function getSymbolAtPosition(editor, position) {
    position = position || editor.getPosition();
    var offset = state.currentTextModel.getOffsetAt(position);
    return getDefinition(state.sourceFileModel, offset) || getReference(state.sourceFileModel, offset);
}
function getReferencesHtmlAtPosition(editor) {
    var definition = getSymbolAtPosition(editor);
    if (!definition) {
        return Promise.resolve(undefined);
    }
    return getFindAllReferencesHtml(definition.projectId, definition.symbol);
}
function getTargetAtPosition(editor) {
    var position = editor.getPosition();
    var offset = state.currentTextModel.getOffsetAt(position);
    var definition = getDefinition(state.sourceFileModel, offset);
    if (definition) {
        return getFindAllReferencesHtml(definition.projectId, definition.symbol);
    }
    var reference = getReference(state.sourceFileModel, offset);
    if (reference) {
        return getDefinitionLocation(reference.projectId, reference.symbol);
    }
    return Promise.resolve(undefined);
}
//function openEditor(input) {
//    alert(input.resource);
//    var uri = input.resource;
//    return callServer(uri, function(data) {
//        state.editor.setValue(data.contents);
//    });
//}
function openEditor(input) {
    return __awaiter(this, void 0, void 0, function () {
        var definitionLocation;
        return __generator(this, function (_a) {
            switch (_a.label) {
                case 0: return [4 /*yield*/, getDefinitionLocation(input.resource.projectId, input.resource.symbol)];
                case 1:
                    definitionLocation = _a.sent();
                    return [2 /*return*/, openEditorForLocation(definitionLocation)];
            }
        });
    });
}
function openEditorForLocation(definitionLocation) {
    return __awaiter(this, void 0, void 0, function () {
        var model, monacoPosition, position;
        return __generator(this, function (_a) {
            switch (_a.label) {
                case 0:
                    if (!(typeof definitionLocation === "string")) return [3 /*break*/, 2];
                    return [4 /*yield*/, updateReferences(definitionLocation)];
                case 1:
                    _a.sent();
                    return [3 /*break*/, 3];
                case 2:
                    model = createModelFrom(definitionLocation.contents, definitionLocation.projectId, definitionLocation.filePath);
                    state.sourceFileModel = definitionLocation;
                    state.editor.setModel(model);
                    // TODO: add try/catch, refactor the logic out
                    state.editor.focus();
                    if (definitionLocation.span) {
                        monacoPosition = state.currentTextModel.getPositionAt(definitionLocation.span.position);
                        position = { lineNumber: monacoPosition.lineNumber, column: monacoPosition.column };
                        state.editor.revealPositionInCenter(position);
                        state.editor.setPosition(position);
                        state.editor.deltaDecorations([], [
                            {
                                range: new monaco.Range(position.lineNumber, 1, position.lineNumber, 1),
                                options: { className: 'highlightLine', isWholeLine: true }
                            }
                        ]);
                        state.editor.setSelection({ startLineNumber: position.lineNumber, startColumn: position.column, endLineNumber: position.lineNumber, endColumn: position.column + definitionLocation.span.length });
                    }
                    _a.label = 3;
                case 3: return [2 /*return*/, monaco.Promise.as(null)];
            }
        });
    });
}
var models;
//var models = {
//};
function createModelFrom(content, project, file) {
    if (state.currentTextModel) {
        state.currentTextModel.dispose();
    }
    var key = project + "/" + file;
    state.currentTextModel = monaco.editor.createModel(content, 'cdx', monaco.Uri.parse(key));
    return state.currentTextModel;
}
function createMonacoEditorAndDisplayFileContent(project, file, sourceFile, lineNumber) {
    state.editor = undefined;
    state.sourceFileModel = sourceFile;
    if (!state.editor) {
        require.config({ paths: { 'vs': 'node_modules/monaco-editor/dev/vs' } });
        var editorPane = document.getElementById('editorPane');
        if (editorPane) {
            require(['vs/editor/editor.main'], function () {
                var SymbolicUri = (function (_super) {
                    __extends(SymbolicUri, _super);
                    function SymbolicUri() {
                        return _super !== null && _super.apply(this, arguments) || this;
                    }
                    return SymbolicUri;
                }(monaco.Uri));
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
                });
                state.editor.onMouseDown(function (e) {
                    if (e.event.ctrlKey) {
                        var target = getTargetAtPosition(state.editor).then(function (definitionLocation) {
                            openEditorForLocation(definitionLocation);
                        }, function (e) { });
                    }
                });
                state.editor.onMouseMove(function (e) {
                    if (e.event.ctrlKey) {
                        var symbol = getSymbolAtPosition(state.editor, e.target.position);
                        if (symbol) {
                            var start = state.editor.getModel().getPositionAt(symbol.span.position);
                            var end = state.editor.getModel().getPositionAt(symbol.span.position + symbol.span.length);
                            state.ctrlClickLinkDecorations = state.editor.deltaDecorations(state.ctrlClickLinkDecorations || [], [
                                {
                                    range: new monaco.Range(start.lineNumber, start.column, end.lineNumber, end.column),
                                    options: { inlineClassName: 'blueLink' }
                                }
                            ]);
                            return;
                        }
                    }
                    if (state.ctrlClickLinkDecorations) {
                        state.ctrlClickLinkDecorations = state.editor.deltaDecorations(state.ctrlClickLinkDecorations || [], []);
                    }
                });
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
                        var referencesHtml = getReferencesHtmlAtPosition(ed)
                            .then(function (html) {
                            if (html) {
                                updateReferences(html);
                            }
                        }, function (e) { });
                    }
                });
                var contentNode = document.createElement('div');
                contentNode.innerHTML = 'My content widget';
                contentNode.style.background = 'grey';
                contentNode.style.top = '50px';
                var contentWidget = {
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
                var position;
                var length = 0;
                if (sourceFile.span) {
                    position = state.currentTextModel.getPositionAt(sourceFile.span.position);
                    length = sourceFile.span.length;
                }
                else if (lineNumber) {
                    position = { lineNumber: lineNumber, column: 1 };
                }
                if (position) {
                    state.editor.revealPositionInCenter(position);
                    state.editor.setPosition(position);
                    state.editor.deltaDecorations([], [
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
