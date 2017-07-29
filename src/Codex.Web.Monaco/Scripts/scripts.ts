/// <reference path="../node_modules/@types/jquery/index.d.ts"/>
/// <reference path="../node_modules/monaco-editor/monaco.d.ts"/>
/// <reference path="rpc.ts"/>
/// <reference path="types.ts"/>
/// <reference path="state.ts"/>
/// <reference path="search.ts" />

function ReplaceCurrentState() {
    history.replaceState(state.currentState, state.currentState.windowTitle, getUrlForState(state.currentState));
    setPageTitle(state.currentState.windowTitle);
}

function OnWindowPopState(event) {
    if (event && event.state) {
        DisplayState(event.state);
    }
}

function UpdateState(stateUpdate) {
    var newState = jQuery.extend({}, state.currentState, stateUpdate);
    NavigateToState(newState);
}

function NavigateToState(state) {
    history.pushState(state, state.windowTitle, getUrlForState(state));
    DisplayState(state);
}

function resetLeftPane() {
    setLeftPane("<div class='note'>Enter a search string. Start with ` for full text search results only.</div>");
}

function DisplayState(newState: CodexWebState) {
    if (!newState) {
        return;
    }

    if (!newState.rightProjectId) {
        newState.rightProjectId = newState.leftProjectId;
    }

    if (!newState.leftProjectId) {
        newState.leftProjectId = newState.rightProjectId;
    }

    if (newState.leftPaneContent == "search") {
        if (!state.currentState || state.currentState.leftPaneContent != "search" || state.currentState.searchText != newState.searchText) {
            LoadSearchCore(newState.searchText);
        }
    } else if (newState.leftPaneContent == "project") {
        if (!state.currentState || state.currentState.leftPaneContent != "project" || state.currentState.leftProjectId != newState.leftProjectId) {
            LoadProjectExplorerCore(newState.leftProjectId);
        }
    } else if (newState.leftPaneContent == "outline") {
        if (!state.currentState || state.currentState.leftPaneContent != "outline" || state.currentState.rightProjectId != newState.rightProjectId || state.currentState.filePath != newState.filePath) {
            if (newState.filePath) {
                LoadDocumentOutlineCore(newState.rightProjectId, newState.filePath);
            } else {
                newState.leftPaneContent = null;
            }
        }
    } else if (newState.leftPaneContent == "namespaces") {
        if (!state.currentState || state.currentState.leftPaneContent != "namespaces" || state.currentState.leftProjectId != newState.leftProjectId) {
            LoadNamespacesCore(newState.leftProjectId);
        }
    } else if (newState.leftPaneContent == "references") {
        if (!state.currentState || state.currentState.leftPaneContent != "references"
            || state.currentState.leftProjectId != newState.leftProjectId
            || state.currentState.leftSymbolId != newState.leftSymbolId
            || state.currentState.projectScope != newState.projectScope) {
            LoadReferencesCore(newState.leftProjectId, newState.leftSymbolId, newState.projectScope);
        }
    }
    else {
        resetLeftPane();
        state.searchBox.value = "";
        state.lastSearchString = "";
    }

    if (newState.rightPaneContent == "file") {
        if (!state.currentState || state.currentState.rightPaneContent != "file" || state.currentState.rightProjectId != newState.rightProjectId || state.currentState.filePath != newState.filePath) {
            LoadSourceCodeCore(newState.rightProjectId, newState.filePath, newState.rightSymbolId, newState.lineNumber);
        }
    } else if (newState.rightPaneContent == "symbol") {
        if (!state.currentState || state.currentState.rightPaneContent != "symbol" || state.currentState.rightProjectId != newState.rightProjectId || state.currentState.filePath != newState.filePath || state.currentState.rightSymbolId != newState.rightSymbolId) {
            if (newState.rightSymbolId) {
                LoadDefinitionCore(newState.rightProjectId, newState.rightSymbolId);
            } else {
                LoadSourceCodeCore(newState.rightProjectId, newState.filePath, newState.rightSymbolId, newState.lineNumber);
            }
        }
    } else if (newState.rightPaneContent == "line") {
        if (!state.currentState || state.currentState.rightPaneContent != "line" || state.currentState.rightProjectId != newState.rightProjectId || state.currentState.filePath != newState.filePath || state.currentState.lineNumber != newState.lineNumber) {
            LoadSourceCodeCore(newState.rightProjectId, newState.filePath, null, newState.lineNumber);
        }
    } else if (newState.rightPaneContent == "overview") {
        if (!state.currentState || state.currentState.rightPaneContent != "overview") {
            LoadOverviewCore();
        }
    } else if (newState.rightPaneContent == "about") {
        if (!state.currentState || state.currentState.rightPaneContent != "about") {
            LoadAboutCore();
        }
    }
    else {
        setRightPane();
    }

    setPageTitle(newState.windowTitle);

    state.currentState = newState;
}

function updateReferences(referencesHtml: string) {
    setLeftPane(referencesHtml);
}

function setLeftPane(text) {
    if (!text) {
        text = "<div></div>";
    }

    var leftPane = document.getElementById("leftPane");
    leftPane.innerHTML = text;
}

function setRightPane(text?: string, isEditor: boolean = false) {
    if (state.rightPaneIsEditor == isEditor) {
        return;
    }

    state.rightPaneIsEditor = isEditor;

    if (!text) {
        text = "<div></div>";
    }

    var rightPane = document.getElementById("rightPane");
    rightPane.innerHTML = text;
}

function setPageTitle(title) {
    if (!title) {
        title = state.defaultWindowTitle;
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
        windowTitle: state.defaultWindowTitle,
        rightPaneContent: "overview",
    });
}

function LoadAbout() {
    UpdateState({
        rightProjectId: null,
        filePath: null,
        rightSymbolId: null,
        lineNumber: null,
        windowTitle: state.defaultWindowTitle,
        rightPaneContent: "about",
    });
}

function handleError(e: any) {
    setRightPane("<div class='note'>" + e + "</div>");
}

function loadRightPaneFrom(url: string) {
    rpc.server<string>(url).then(
        data => setRightPane(data),
        e => setRightPane("<div class='note'>" + e + "</div>"));
}

function loadLeftPaneFrom(url: string) {
    rpc.server<string>(url).then(
        data => setLeftPane(data),
        e => setLeftPane("<div class='note'>" + e + "</div>"));
}

function LoadOverviewCore() {
    loadRightPaneFrom("/overview/");
}

function LoadAboutCore() {
    loadRightPaneFrom("/about/");
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
    // TODO: the 'definitionlocation' was removed.
    var url = state.codexWebRootPrefix + "/definitionlocation/" + encodeURI(project) + "/?symbolId=" + encodeURIComponent(symbolId);
    rpc.server(url).then(function (data: any) {
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
    var url = state.codexWebRootPrefix + "/references/" + encodeURI(project) + "/?symbolId=" + encodeURIComponent(symbolId);
    if (projectScope)
    {
        url = appendParam(url, "projectScope", projectScope);
    }

    loadLeftPaneFrom(url);
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

    var title = state.currentState.windowTitle;
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

function LoadSourceCodeCore(project: string, file: string, symbolId: string, lineNumber?: number) {
    //if (currentState.rightProjectId == project && currentState.filePath == file) {
    //    GoToSymbolOrLineNumber(symbolId, lineNumber);
    //    return;
    //}

    var url = `/source/${encodeURI(project)}/?filename=${encodeURIComponent(file)}&partial=true`;
    FillRightPane(url, symbolId, lineNumber, project, file);
}

async function FillRightPane(url: string, symbolId: string, lineNumber: number, project: string, file: string) {
    // try {
        let data = await rpc.server(url);
        displayFile(data, symbolId, lineNumber);

        let sourceFileData = await rpc.getSourceFileContents(project, file);
        if (symbolId) {
            let definitionSpan = rpc.getDefinitionForSymbol(sourceFileData, symbolId);
            if (definitionSpan) {
                sourceFileData.span = <LineSpan>definitionSpan.span;
            }
        }

        // createMonacoEditorAndDisplayFileContent(project, file, sourceFileData, lineNumber);

        if (!state.codexEditor) {
            let result = await CodexEditor.createAsync(new CodexWebServer(), new CodexWebPage(), document.getElementById('editorPane'));
            state.codexEditor = result;
        }

        let targetLocation: TargetEditorLocation = undefined;
        if (sourceFileData.span) {
            targetLocation = {kind: 'span', value: sourceFileData.span};
        }

        if (sourceFileData.contents) {
            state.codexEditor.openFile(sourceFileData, targetLocation);
        }
        
        let bottomPaneInnerHtml = document.getElementById("bottomPaneHidden").innerHTML;
        bottomPaneInnerHtml = replaceAll(replaceAll(replaceAll(replaceAll(bottomPaneInnerHtml,
            "{filePath}", file),
            "{projectId}", project),
            "{repoRelativePath}", sourceFileData.repoRelativePath || ""),
            "{webLink}", sourceFileData.webLink || "");
        document.getElementById("bottomPane").innerHTML = bottomPaneInnerHtml;
    // }
    // catch (e) {
    //     setRightPane("<div class='note'>" + e + "</div>");
    // }
}

function displayFile(data, symbolId, lineNumber) {
    //if (data.contents) {
    //    loadMonacoEditor(data.contents);
    //}

    setRightPane(data, true);

    //var filePath = getFilePath();
    //if (filePath && filePath !== state.currentState.filePath) {
    //    state.currentState.filePath = filePath;
    //    state.currentState.windowTitle = filePath;
    //    ReplaceCurrentState();
    //}
    //
    //GoToSymbolOrLineNumber(symbolId, lineNumber);
    //
    //addToolbar();
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
    rpc.server(url).then(function (data) {
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
    loadLeftPaneFrom(url);
}

function LoadNamespaces(project) {
    UpdateState({
        leftProjectId: project,
        leftPaneContent: "namespaces",
    });
}

function LoadNamespacesCore(project) {
    var url = "/namespaces/" + encodeURI(project) + "/";
    loadLeftPaneFrom(url);
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
    LoadDocumentOutline(state.currentState.rightProjectId, state.currentState.filePath);
}

function showNamespaceExplorer() {
    var projectId = state.currentState.rightProjectId;
    if (!projectId) {
        projectId = state.currentState.leftProjectId;
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

function getEditorPane(): HTMLElement {
    return document.getElementById("editorPane");
}

function getEditorWrapperPane(): HTMLElement {
    return document.getElementById("editorPaneWrapper");
}

function getFilePath(): string {    
    let editorPane = getEditorPane();
    return editorPane && editorPane.getAttribute("data-filepath");
}

function selectFile(a) {
    var selected = state.selectedFile;
    if (selected === a) {
        return;
    }

    if (selected && selected.classList) {
        selected.classList.remove("selectedFilename");
    }

    state.selectedFile = a;
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
    if (state.currentState.rightProjectId && state.currentState.filePath) {
        LoadSourceCode(state.currentState.rightProjectId, state.currentState.filePath, symbolId, null);
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

function replaceAll(originalString: string, oldValue: string, newValue: string, ignoreCase: boolean = false) {
    //
    // if invalid data, return the original string
    //
    if ((originalString == null) || (oldValue == null) || (newValue == null) || (oldValue.length == 0))
        return (originalString);
    //
    // set search/replace flags
    //
    var Flags: string = (ignoreCase) ? "gi" : "g";
    //
    // apply regex escape sequence on pattern (oldValue)
    //
    var pattern = oldValue.replace(/[-\[\]\/\{\}\(\)\*\+\?\.\\\^\$\|]/g, "\\$&");
    //
    // replace oldValue with newValue
    //
    var str = originalString.replace(new RegExp(pattern, Flags), newValue);
    return (str);
}