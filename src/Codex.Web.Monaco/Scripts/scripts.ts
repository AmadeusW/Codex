/// <reference path="../node_modules/@types/jquery/index.d.ts"/>
/// <reference path="../node_modules/monaco-editor/monaco.d.ts"/>
/// <reference path="rpc.ts"/>
/// <reference path="editor.ts"/>

var defaultWindowTitle = "Index";
var editor;
var codexWebRootPrefix = "";
var currentTextModel;
var sourceFileModel;
var currentState: any;

var searchBox: any;
var lastSearchString;

var selectedFile;

// For type safety reasons. The function defined in another js file right now.
declare function LoadSearchCore(searchText);

function ReplaceCurrentState() {
    history.replaceState(currentState, currentState.windowTitle, getUrlForState(currentState));
    setPageTitle(currentState.windowTitle);
}

function OnWindowPopState(event) {
    if (event && event.state) {
        DisplayState(event.state);
    }
}

function UpdateState(stateUpdate) {
    var newState = jQuery.extend({}, currentState, stateUpdate);
    NavigateToState(newState);
}

function NavigateToState(state) {
    history.pushState(state, state.windowTitle, getUrlForState(state));
    DisplayState(state);
}

function resetLeftPane() {
    setLeftPane("<div class='note'>Enter a search string. Start with ` for full text search results only.</div>");
}

function DisplayState(state) {
    if (!state) {
        return;
    }

    if (!state.rightProjectId) {
        state.rightProjectId = state.leftProjectId;
    }

    if (!state.leftProjectId) {
        state.leftProjectId = state.rightProjectId;
    }

    if (state.leftPaneContent == "search") {
        if (!currentState || currentState.leftPaneContent != "search" || currentState.searchText != state.searchText) {
            LoadSearchCore(state.searchText);
        }
    } else if (state.leftPaneContent == "project") {
        if (!currentState || currentState.leftPaneContent != "project" || currentState.leftProjectId != state.leftProjectId) {
            LoadProjectExplorerCore(state.leftProjectId);
        }
    } else if (state.leftPaneContent == "outline") {
        if (!currentState || currentState.leftPaneContent != "outline" || currentState.rightProjectId != state.rightProjectId || currentState.filePath != state.filePath) {
            if (state.filePath) {
                LoadDocumentOutlineCore(state.rightProjectId, state.filePath);
            } else {
                state.leftPaneContent = null;
            }
        }
    } else if (state.leftPaneContent == "namespaces") {
        if (!currentState || currentState.leftPaneContent != "namespaces" || currentState.leftProjectId != state.leftProjectId) {
            LoadNamespacesCore(state.leftProjectId);
        }
    } else if (state.leftPaneContent == "references") {
        if (!currentState || currentState.leftPaneContent != "references"
            || currentState.leftProjectId != state.leftProjectId
            || currentState.leftSymbolId != state.leftSymbolId
            || currentState.projectScope != state.projectScope) {
            LoadReferencesCore(state.leftProjectId, state.leftSymbolId, state.projectScope);
        }
    }
    else {
        resetLeftPane();
        searchBox.value = "";
        lastSearchString = "";
    }

    if (state.rightPaneContent == "file") {
        if (!currentState || currentState.rightPaneContent != "file" || currentState.rightProjectId != state.rightProjectId || currentState.filePath != state.filePath) {
            LoadSourceCodeCore(state.rightProjectId, state.filePath, state.rightSymbolId, state.lineNumber);
        }
    } else if (state.rightPaneContent == "symbol") {
        if (!currentState || currentState.rightPaneContent != "symbol" || currentState.rightProjectId != state.rightProjectId || currentState.filePath != state.filePath || currentState.rightSymbolId != state.rightSymbolId) {
            if (state.rightSymbolId) {
                LoadDefinitionCore(state.rightProjectId, state.rightSymbolId);
            } else {
                LoadSourceCodeCore(state.rightProjectId, state.filePath, state.rightSymbolId, state.lineNumber);
            }
        }
    } else if (state.rightPaneContent == "line") {
        if (!currentState || currentState.rightPaneContent != "line" || currentState.rightProjectId != state.rightProjectId || currentState.filePath != state.filePath || currentState.lineNumber != state.lineNumber) {
            LoadSourceCodeCore(state.rightProjectId, state.filePath, null, state.lineNumber);
        }
    } else if (state.rightPaneContent == "overview") {
        if (!currentState || currentState.rightPaneContent != "overview") {
            LoadOverviewCore();
        }
    } else if (state.rightPaneContent == "about") {
        if (!currentState || currentState.rightPaneContent != "about") {
            LoadAboutCore();
        }
    }
    else {
        setRightPane();
    }

    setPageTitle(state.windowTitle);

    currentState = state;
}

function setLeftPane(text) {
    if (!text) {
        text = "<div></div>";
    }

    var leftPane = document.getElementById("leftPane");
    leftPane.innerHTML = text;
}

function setRightPane(text?: string) {
    if (!text) {
        text = "<div></div>";
    }

    var rightPane = document.getElementById("rightPane");
    rightPane.innerHTML = text;
}

function setPageTitle(title) {
    if (!title) {
        title = defaultWindowTitle;
    }

    document.title = title;
}

function GoToLine(lineNumber) {
    UpdateState({
        lineNumber: lineNumber,
        rightPaneContent: "line",
    });
}

function LoadOverview() {
    UpdateState({
        rightProjectId: null,
        filePath: null,
        rightSymbolId: null,
        lineNumber: null,
        windowTitle: defaultWindowTitle,
        rightPaneContent: "overview",
    });
}

function LoadAbout() {
    UpdateState({
        rightProjectId: null,
        filePath: null,
        rightSymbolId: null,
        lineNumber: null,
        windowTitle: defaultWindowTitle,
        rightPaneContent: "about",
    });
}

function LoadOverviewCore() {
    var url = "/overview/";
    callServer(url, function (data) {
        setRightPane(data);
    }, function (error) {
        setRightPane("<div class='note'>" + error + "</div>");
    });
}

function LoadAboutCore() {
    var url = "/about/";
    callServer(url, function (data) {
        setRightPane(data);
    }, function (error) {
        setRightPane("<div class='note'>" + error + "</div>");
    });
}

function LoadDefinition(project, symbolId) {
    UpdateState({
        rightProjectId: project,
        filePath: null,
        rightSymbolId: symbolId,
        rightPaneContent: "symbol",
    });
}

function LoadDefinitionCore(project, symbolId) {
    var url = codexWebRootPrefix + "/definitionlocation/" + encodeURI(project) + "/?symbolId=" + encodeURIComponent(symbolId);
    callServer(url, function (data) {
        LoadSourceCodeCore(data.projectId, data.filename, symbolId);
        //if (startsWith(data, "<!--Definitions-->")) {
        //    setLeftPane(data);
        //    return;
        //}

        //displayFile(data, symbolId, null);
        //
        //var contentsUrl = codexWebRootPrefix + "/definitionscontents/" + encodeURI(project) + "/?symbolId=" + encodeURIComponent(symbolId);
        //callServer(contentsUrl, function(contentData) {
        //    var filePath = getFilePath();
        //    if (filePath) {
        //        loadMonacoEditorWithSourceFile(project, filePath, contentData);
        //    }
        //});
    }, function (error) {
        setRightPane("<div class='note'>" + error + "</div>");
    });
}

function LoadReferences(project, symbolId, projectScope) {
    UpdateState({
        leftProjectId: project,
        leftSymbolId: symbolId,
        projectScope: projectScope || null,
        lineNumber: null,
        leftPaneContent: "references",
        rightPaneContent: "symbol",
    });
}

function LoadReferencesCore(project, symbolId, projectScope) {
    var url = codexWebRootPrefix + "/references/" + encodeURI(project) + "/?symbolId=" + encodeURIComponent(symbolId);
    if (projectScope)
    {
        url = appendParam(url, "projectScope", projectScope);
    }

    callServer(url, function (data) {
        setLeftPane(data);
    }, function (error) {
        setLeftPane("<div class='note'>" + error + "</div>");
    });
}

function LoadSourceCode(project, file, symbolId, lineNumber) {
    var whichContent = "symbol";
    if (!symbolId) {
        if (lineNumber) {
            whichContent = "line";
        } else {
            whichContent = "file";
        }
    }

    var title = currentState.windowTitle;
    if (file) {
        title = file;
    }

    UpdateState({
        rightProjectId: project,
        filePath: file,
        rightSymbolId: symbolId,
        lineNumber: lineNumber,
        windowTitle: title,
        rightPaneContent: whichContent,
    });
}
function LoadSourceCodeCore(project: string, file: string, symbolId: string, lineNumber?) {
    //if (currentState.rightProjectId == project && currentState.filePath == file) {
    //    GoToSymbolOrLineNumber(symbolId, lineNumber);
    //    return;
    //}

    var url = `/source/${encodeURI(project)}/?filename=${encodeURIComponent(file)}&partial=true`;
    var contentsUrl = `/sourcecontent/${encodeURI(project)}/?fileName=${encodeURIComponent(file)}`;
    FillRightPane(url, symbolId, lineNumber, contentsUrl, project, file);
}

function FillRightPane(url: string, symbolId: string, lineNumber, contentsUrl, project: string, file: string) {
    callServer(url, data => {
        displayFile(data, symbolId, lineNumber);

        if (contentsUrl) {
            callServer(contentsUrl, (sourceFileData: SourceFileContentsModel) => {
                    var filePath = getFilePath();
                    if (filePath) {
                        let definitionUrl = `/definitionlocation/${encodeURI(project)}/?symbolId=${encodeURIComponent(symbolId)}`;
                        callServer(definitionUrl, (sfd: SourceFileContentsModel) => {
                            // TODO: this is extremely strange to get this stuff!!!
                            sourceFileData.span = sfd.span;
                            createMonacoEditorAndDisplayFileContent(project, file, sourceFileData);
                        },
                        () => {});

                    }
                },
                () => {});
        }
    }, error => {
        setRightPane("<div class='note'>" + error + "</div>");
    });
}

function displayFile(data, symbolId, lineNumber) {
    //if (data.contents) {
    //    loadMonacoEditor(data.contents);
    //}

    setRightPane(data);

    var filePath = getFilePath();
    if (filePath && filePath !== currentState.filePath) {
        currentState.filePath = filePath;
        currentState.windowTitle = filePath;
        ReplaceCurrentState();
    }

    GoToSymbolOrLineNumber(symbolId, lineNumber);

    addToolbar();
    trackActiveItemInSolutionExplorer();
}

function GoToSymbolOrLineNumber(symbolId, lineNumber) {
    var blurLine = false;
    if (symbolId) {
        var symbolElement = $("#" + symbolId);
        if (symbolElement[0]) {
            symbolElement.scrollTop();
            symbolElement.focus();
        }
        else if (!lineNumber)
        {
            lineNumber = 1;
            symbolId = undefined;
            blurLine = true;
        }
    }

    if (lineNumber && !symbolId) {
        var lineNumberElement = $("#l" + lineNumber);
        if (lineNumberElement[0]) {
            lineNumberElement.scrollTop();
            lineNumberElement.focus();
            if (blurLine)
            {
                lineNumberElement.blur();
            }
        }
    }
}

function LoadProjectExplorer(project) {
    UpdateState({
        leftProjectId: project,
        leftPaneContent: "project",
    });
}

function LoadProjectExplorerCore(project) {
    var url = "/projectexplorer/" + encodeURI(project) + "/";
    callServer(url, function (data) {
        setLeftPane(data);
        trackActiveItemInSolutionExplorer();
    }, function (error) {
        setLeftPane("<div class='note'>" + error + "</div>");
    });
}

function LoadDocumentOutline(project, filePath) {
    UpdateState({
        rightProjectId: project,
        filePath: filePath,
        leftPaneContent: "outline",
    });
}

function LoadDocumentOutlineCore(project, file) {
    var url = "/documentoutline/" + encodeURI(project) + "/?filePath=" + encodeURIComponent(file);
    callServer(url, function (data) {
        setLeftPane(data);
    }, function (error) {
        setLeftPane("<div class='note'>" + error + "</div>");
    });
}

function LoadNamespaces(project) {
    UpdateState({
        leftProjectId: project,
        leftPaneContent: "namespaces",
    });
}

function LoadNamespacesCore(project) {
    var url = "/namespaces/" + encodeURI(project) + "/";
    callServer(url, function (data) {
        setLeftPane(data);
    }, function (error) {
        setLeftPane("<div class='note'>" + error + "</div>");
    });
}

function server<T>(url: string): monaco.Promise<T> {
    function onSuccess(data) {
        return data;
    }

    $.ajax({
        url: url,
        type: "GET",
        success: onSuccess,
        error: function (jqXHR, textStatus, errorThrown) {
            if (textStatus !== "abort") {
                //errorCallback(jqXHR + "\n" + textStatus + "\n" + errorThrown);
            }
        }
    });

    return new monaco.Promise(onSuccess, () => { });
    //let promise = monaco.Promise()
}

function callServer(url, callback, errorCallback) {
    $.ajax({
        url: url,
        type: "GET",
        success: function (data) {
            callback(data);
        },
        error: function (jqXHR, textStatus, errorThrown) {
            if (textStatus !== "abort") {
                errorCallback(jqXHR + "\n" + textStatus + "\n" + errorThrown);
            }
        }
    });
}

function ToggleExpandCollapse(headerElement) {
    var collapsible = headerElement.nextSibling;
    if (collapsible.style.display == "none") {
        collapsible.style.display = "block";
        headerElement.style.backgroundImage = "url(../../content/icons/minus.png)";
    } else {
        collapsible.style.display = "none";
        headerElement.style.backgroundImage = "url(../../content/icons/plus.png)";
    }
}

function ToggleFolderIcon(headerElement) {
    var folderIcon = headerElement.firstChild;
    if (!folderIcon) {
        return;
    }

    if (endsWith(folderIcon.src, "202.png")) {
        folderIcon.src = "../../content/icons/201.png";
    } else if (endsWith(folderIcon.src, "201.png")) {
        folderIcon.src = "../../content/icons/202.png";
    }
}

function startsWith(text, prefix) {
    if (!text || !prefix) {
        return false;
    }

    if (prefix.length > text.length) {
        return false;
    }

    var slice = text.slice(0, prefix.length);
    return slice == prefix;
}

function endsWith(text, suffix) {
    if (!text || !suffix) {
        return false;
    }

    if (suffix.length > text.length) {
        return false;
    }

    var slice = text.slice(text.length - suffix.length, text.length);
    return slice == suffix;
}

function getUrlForState(state) {
    var url = "?";
    var hasProjectId = false;

    if (state.leftPaneContent == "search" && state.searchText) {
        url = appendParam(url, "query", state.searchText);
    } else if (state.leftPaneContent == "project" && state.leftProjectId) {
        url = appendParam(url, "leftProject", state.leftProjectId);
        hasProjectId = true;
    } else if (state.leftPaneContent == "references" && state.leftProjectId && state.leftSymbolId) {
        url = appendParam(url, "leftProject", state.leftProjectId);
        url = appendParam(url, "leftSymbol", state.leftSymbolId);
        if (state.projectScope)
        {
            url = appendParam(url, "projectScope", state.projectScope);
        }

        hasProjectId = true;
    } else if (state.leftPaneContent == "outline" && state.rightProjectId && state.filePath) {
        url = appendParam(url, "left", "outline");
    } else if (state.leftPaneContent == "namespaces" && state.leftProjectId) {
        url = appendParam(url, "left", "namespaces");
        url = appendParam(url, "leftProject", state.leftProjectId);
        hasProjectId = true;
    }

    if (state.rightPaneContent == "file" && state.rightProjectId && state.filePath) {
        if (state.leftProjectId !== state.rightProjectId || !hasProjectId) {
            url = appendParam(url, "rightProject", state.rightProjectId);
        }

        url = appendParam(url, "file", state.filePath);
    } else if (state.rightPaneContent == "symbol" && state.rightProjectId && state.rightSymbolId) {
        if (state.leftProjectId !== state.rightProjectId || !hasProjectId) {
            url = appendParam(url, "rightProject", state.rightProjectId);
        }

        if (state.filePath) {
            url = appendParam(url, "file", state.filePath);
        }

        if (state.leftSymbolId !== state.rightSymbolId) {
            url = appendParam(url, "rightSymbol", state.rightSymbolId);
        }
    } else if (state.rightPaneContent == "line" && state.rightProjectId && state.filePath && state.lineNumber) {
        if (state.leftProjectId !== state.rightProjectId || !hasProjectId) {
            url = appendParam(url, "rightProject", state.rightProjectId);
        }

        url = appendParam(url, "file", state.filePath);
        url = appendParam(url, "line", state.lineNumber);
    } else if (state.rightPaneContent == "about") {
        url = "about";
    }

    if (url.length == 1) {
        return null;
    }

    return url;
}

function appendParam(url, name, value) {
    var result = url;
    if (result.length > 1) {
        result += "&";
    }

    result += name + "=" + encodeURIComponent(value);

    return result;
}

function addToolbar() {
    var editorPane = document.getElementById("sourceCode");
    if (!editorPane) {
        return;
    }

    var documentOutlineButton = document.createElement('img');
    documentOutlineButton.setAttribute('src', '../../content/icons/DocumentOutline.png');
    documentOutlineButton.title = "Document Outline";
    documentOutlineButton.className = 'documentOutlineButton';
    documentOutlineButton.onclick = showDocumentOutline;
    editorPane.appendChild(documentOutlineButton);

    var projectExplorerButton = document.createElement('img');
    var projectExplorerIcon = '../../content/icons/CSharpProjectExplorer.png';

    projectExplorerButton.setAttribute('src', projectExplorerIcon);
    projectExplorerButton.title = "Project Explorer";
    projectExplorerButton.className = 'projectExplorerButton';
    projectExplorerButton.onclick = function () { document.getElementById('projectExplorerLink').click(); };
    editorPane.appendChild(projectExplorerButton);

    var namespaceExplorerButton = document.createElement('img');
    namespaceExplorerButton.setAttribute('src', '../../content/icons/NamespaceExplorer.png');
    namespaceExplorerButton.title = "Namespace Explorer";
    namespaceExplorerButton.className = 'namespaceExplorerButton';
    namespaceExplorerButton.onclick = showNamespaceExplorer;
    //editorPane.appendChild(namespaceExplorerButton);
}

function showDocumentOutline() {
    LoadDocumentOutline(currentState.rightProjectId, currentState.filePath);
}

function showNamespaceExplorer() {
    var projectId = currentState.rightProjectId;
    if (!projectId) {
        projectId = currentState.leftProjectId;
    }

    if (projectId) {
        LoadNamespaces(projectId);
    }
}

function trackActiveItemInSolutionExplorer() {
    var projectExplorer = document.getElementById("projectExplorer");
    if (!projectExplorer) {
        return;
    }

    var rootFolderDiv = projectExplorer.firstChild;
    if (rootFolderDiv && ((<any>rootFolderDiv).className == "projectCS" || (<any>rootFolderDiv).className == "projectVB")) {
        rootFolderDiv = (<any>rootFolderDiv).nextElementSibling;
        if (rootFolderDiv) {
            var filePath = getFilePath();
            if (filePath) {
                selectItem(rootFolderDiv, filePath.split("\\"));
            }
        }
    }
}

function selectItem(div, parts) {
    var text = parts[0];
    var found = null;
    for (var i = 0; i < div.children.length; i++) {
        var child = div.children[i];
        if (getInnerText(child) == text) {
            found = child;
            break;
        }
    }

    if (!found) {
        return;
    }

    if (parts.length == 1 && found.tagName == "A") {
        selectFile(found);
    }
    else if (parts.length > 1 && found.tagName == "DIV") {
        found = found.nextElementSibling;
        expandFolderIfNeeded(found);
        selectItem(found, parts.slice(1));
    }
}

function getInnerText(element) {
    if (typeof element.innerText !== "undefined") {
        return element.innerText;
    } else {
        return element.textContent;
    }
}

function expandFolderIfNeeded(folder) {
    if (folder.style.display != "block" && folder && folder.previousSibling && folder.previousSibling.onclick) {
        folder.previousSibling.onclick();
    }
}

function getFilePath() {
    var editorPane = document.getElementById("editorPane");
    if (!editorPane) {
        return;
    }

    var filePath = editorPane.getAttribute("data-filepath");
    return filePath;
}

function selectFile(a) {
    var selected = selectedFile;
    if (selected === a) {
        return;
    }

    if (selected && selected.classList) {
        selected.classList.remove("selectedFilename");
    }

    selectedFile = a;
    if (a) {
        if (a.classList) {
            a.classList.add("selectedFilename");
        }

        scrollIntoViewIfNeeded(a);
    }
}

function scrollIntoViewIfNeeded(element) {
    var topOfPage = window.pageYOffset || document.documentElement.scrollTop || document.body.scrollTop;
    var heightOfPage = window.innerHeight || document.documentElement.clientHeight || document.body.clientHeight;
    var elY = 0;
    var elH = 0;

    if ((<any>document).layers) {
        elY = element.y;
        elH = element.height;
    }
    else {
        for (var p = element; p && p.tagName != 'BODY'; p = p.offsetParent) {
            elY += p.offsetTop;
        }

        elH = element.offsetHeight;
    }

    if ((topOfPage + heightOfPage) < (elY + elH)) {
        element.scrollIntoView(false);
    }
    else if (elY < topOfPage) {
        element.scrollIntoView(true);
    }
}

// called when clicking on a tree item in Document Outline
function S(symbolId) {
    if (currentState.rightProjectId && currentState.filePath) {
        LoadSourceCode(currentState.rightProjectId, currentState.filePath, symbolId, null);
    }
}

// called when clicking on a reference to a symbol in source code
function D(projectId, symbolId) {
    LoadDefinition(projectId, symbolId);
}

// called when clicking on a definition of a symbol in source code
function R(projectId, symbolId, projectScope) {
    LoadReferences(projectId, symbolId, projectScope);
}

var currentSelection = null;

// highlight references
function t(sender) {
    var classname = sender.className;

    var elements;
    if (currentSelection) {
        elements = document.getElementsByClassName(currentSelection);
        for (var i = 0; i < elements.length; i++) {
            elements[i].style.background = "transparent";
        }

        if (classname == currentSelection) {
            currentSelection = null;
            return;
        }
    }

    currentSelection = classname;

    elements = document.getElementsByClassName(currentSelection);
    for (var i = 0; i < elements.length; i++) {
        elements[i].style.background = "cyan";
    }
}