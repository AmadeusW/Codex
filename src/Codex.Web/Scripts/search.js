var lastQuery = null;
var lastSearchString = "";
var searchTimerID = -1;
var searchBox = null;
var selectedResultGroup = null; // model
var selectedResult = null;      // model
var selectedUIElement = null;   // presentation

function onBodyLoad() {

    var anchor = document.location.hash;
    if (anchor && !document.location.search && document.location.pathname === "/") {
        top.location.replace("http://ddindex/" + anchor);
        return;
    }

    // https://github.com/nathancahill/Split.js
    Split(['#leftPane', '#rightPane'], {
        sizes: ['504px', 'calc(100% - 504px)'],
        gutterSize: 20,
        minSize: 1,
        cursor: 'col-resize'
    });

    ensureSearchBox();

    var link = document.getElementById("feedbackButtonLink");
    link.href = "mailto:" + "codexteam" + "@" + "microsoft" + '.' + "com";

    window.onpopstate = OnWindowPopState;
}

function ensureSearchBox() {
    if (typeof searchBox === "object" && searchBox != null) {
        return;
    }

    searchBox = document.getElementById("search-box");

    lastSearchString = searchBox.value;

    searchBox.focus();

    searchBox.onkeyup = function () {
        if (event && event.keyCode == 13) { // enter
            lastSearchString = "";
            onSearchChange();
        }
    }
    // onkeydown supports repeating when user holds the button
    searchBox.onkeydown = function () {
        if (event) {
            switch (event.keyCode) {
                case 38: // up
                    if (event.ctrlKey) {
                        collapseResult();
                    } else {
                        selectPreviousResult();
                    }
                    return false; // cancel the event so the caret doesn't move
                case 40: // down
                    if (event.ctrlKey) {
                        expandResult();
                    } else {
                        selectNextResult();
                    }
                    return false; // cancel the event so the caret doesn't move
                default:
                    break;
            }
        }
    };

    searchBox.oninput = function () {
        onSearchChange();
    };
}

function onSearchChange() {
    if (lastSearchString && lastSearchString == searchBox.value) {
        return;
    }
    resetSelectedResult();

    if (searchBox.value.length > 2) {
        if (searchTimerID == -1) {
            searchTimerID = setTimeout(runSearch, 200);
        }
    } else {
        setLeftPane("<div class='note'>Enter at least 3 characters.</div>");
        lastSearchString = searchBox.value;
    }
}

// Called when a new search happens and result list is reset
function resetSelectedResult() {
    selectedResultGroup = null;
    selectedResult = null;
    if (selectedUIElement != null) {
        selectedUIElement.removeClass("selectedResult");
        selectedUIElement = null;
    }
}

function expandResult() {
    if (selectedUIElement == null) {
        return;
    }
    if (selectedUIElement.is('.resultItem')) { // Selected item is a single result
        selectedUIElement.parent().trigger("click");
    } else if (selectedUIElement.is('.resultGroupHeader')) { // Selected item is a group
        if (selectedUIElement.next("div").css("display") == "none") {
            selectedUIElement.trigger("click");
        }
    }
}

function collapseResult() {
    if (selectedUIElement == null) {
        return;
    }
    if (selectedUIElement.is('.resultItem')) { // Selected item is a single result
        // Select the parent group and collapse it
        selectedUIElement.removeClass("selectedResult");
        selectedResult = null;
        selectedResultGroup = selectedUIElement.parent().parent().parent().first();
        selectedUIElement = selectedResultGroup.children('.resultGroupHeader')
        selectedUIElement.trigger("click");
        selectedUIElement.addClass("selectedResult");
    } else if (selectedUIElement.is('.resultGroupHeader')) { // Selected item is a group
        if (selectedUIElement.next("div").css("display") != "none") {
            selectedUIElement.trigger("click");
        }
    }
}

function selectNextResult() {
    if (selectedUIElement != null) { // Deselect currently selected element
        selectedUIElement.removeClass("selectedResult");
    }

    if (selectedResultGroup == null || !selectedResultGroup.length) { // Nothing is selected. Select the first group.
        selectedResultGroup = $(".resultGroup").first();
        selectedUIElement = selectedResultGroup.children(".resultGroupHeader").first();
    }
    else {
        if (selectedResultGroup.children().not(".resultGroupHeader").first().css("display") != "none") {
            if (selectedResult == null || !selectedResult.length) { // Select the first result
                var resultContainer = selectedResultGroup.children().not(".resultGroupHeader").first();
                var resultLink = resultContainer.children("a").first();
                selectedResult = resultLink.children(".resultItem").first();
                selectedUIElement = selectedResult;
            }
            else { // Select the next result
                var resultLink = selectedResult.parent().next();
                if (resultLink.is('a')) {
                    selectedResult = resultLink.children(".resultItem").first();
                    selectedUIElement = selectedResult;
                }
                else { // There are no more siblings. Go to the next group
                    selectedResult = null;
                    selectedResultGroup = selectedResultGroup.next(".resultGroup");
                    if (selectedResultGroup.is("div")) {
                        selectedUIElement = selectedResultGroup.children(".resultGroupHeader").first();
                    }
                    else {
                        // We've reached the end of the list
                        return;
                    }
                }
            }
        }
        else { // Select the next group
            selectedResult = null; // so that we try to go back to a result
            selectedResultGroup = selectedResultGroup.next(".resultGroup");
            if (selectedResultGroup.is("div")) {
                selectedUIElement = selectedResultGroup.children(".resultGroupHeader").first();
            }
            else {
                // We've reached the end of the list
                return;
            }
        }
    }
    if (selectedUIElement != null && selectedUIElement.length) {
        // Select and scroll to the element
        selectedUIElement.addClass("selectedResult");
        selectedUIElement[0].scrollIntoViewIfNeeded();
    }
}

function selectPreviousResult() {
    if (selectedUIElement != null) {
        selectedUIElement.removeClass("selectedResult");
    }

    if (selectedResult == null && selectedResult.length) {
        // Last result
        selectedResultGroup = selectedResultGroup.prev(".resultGroup");
        if (selectedResultGroup.children().not(".resultGroupHeader").first().css("display") != "none") {
            if (selectedResultGroup.is("div")) {
                var resultContainer = selectedResultGroup.children().not(".resultGroupHeader").first();
                var resultLink = resultContainer.children("a").last();
                selectedResult = resultLink.children(".resultItem").first();
                selectedUIElement = selectedResult;
            }
            else {
                // We've reached the beginning of the list
                $("#leftPane").scrollTop(0); // scroll to the very top
                return;
            }
        }
        else {
            // The group we would have went to is collapsed. go to its header
            selectedResult = null;
            selectedUIElement = selectedResultGroup.children(".resultGroupHeader").first();
        }
    }
    else {
        var resultLink = selectedResult.parent().prev();
        if (resultLink.is('a')) {
            // Previous result
            selectedResult = resultLink.children(".resultItem").first();
            selectedUIElement = selectedResult;
        }
        else { // There are no more siblings. Go to the previous group
            selectedResult = null; // so that we try to go back to a result
            if (selectedResultGroup.is("div")) {
                selectedUIElement = selectedResultGroup.children(".resultGroupHeader").first();
            }
            else {
                // We've reached the beginning
                $("#leftPane").scrollTop(0); // scroll to the very top
                return;
            }
        }
    }
    if (selectedUIElement != null && selectedUIElement.length) {
        // Select and scroll to the element
        selectedUIElement.addClass("selectedResult");
        selectedUIElement[0].scrollIntoViewIfNeeded();
    }
}

function LoadSearchCore(searchText) {
    if (searchBox.value != searchText || currentState.leftPaneContent != "search") {
        searchBox.value = searchText;
        searchBox.focus();
        runSearch();
    }
}

function runSearch() {
    // Reset the timerID so we can kick off the next search timeout
    searchTimerID = -1;

    // Is a search currently running? If so abort it first
    if (typeof lastQuery === "object" && lastQuery !== null) {
        lastQuery.abort();
        lastQuery = null;
    }

    // Call the SearchController to fetch the search results and render them as HTML
    lastQuery = $.ajax({
        url: codexWebRootPrefix + "/search/ResultsAsHtml",
        type: "GET",
        data: { "searchTerm": escape(searchBox.value) },
        success: function (data) {
            lastQuery = null;
            lastSearchString = searchBox.value;
            setLeftPane(data);

            if (currentState.leftPaneContent == "search") {
                if (currentState.searchText != lastSearchString) {
                    currentState.searchText = lastSearchString;
                    currentState.windowTitle = lastSearchString;
                    ReplaceCurrentState();
                }
            } else {
                var state = {
                    leftProjectId: currentState.leftProjectId,
                    rightProjectId: currentState.rightProjectId,
                    filePath: currentState.filePath,
                    leftSymbolId: currentState.leftSymbolId,
                    rightSymbolId: currentState.rightSymbolId,
                    lineNumber: currentState.lineNumber,
                    windowTitle: lastSearchString,
                    searchText: lastSearchString,
                    leftPaneContent: "search",
                    rightPaneContent: currentState.rightPaneContent,
                };
                NavigateToState(state);
            }
        },
        error: function (jqXHR, textStatus, errorThrown) {
            if (textStatus !== "abort") {
                setLeftPane(jqXHR + "\n" + textStatus + "\n" + errorThrown);
            }
        }
    });
}