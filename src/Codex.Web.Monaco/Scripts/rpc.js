/// <reference path="../node_modules/@types/jquery/index.d.ts"/>
var codexWebRootPrefix = "";
function serverWithPrefix(url) {
    return server(codexWebRootPrefix + url);
}
function server(url) {
    return new Promise(function (resolve, reject) {
        $.ajax({
            url: url,
            type: "GET",
            success: function (data) { resolve(data); return data; },
            error: function (jqXHR, textStatus, errorThrown) {
                if (textStatus !== "abort") {
                    //errorCallback(jqXHR + "\n" + textStatus + "\n" + errorThrown);
                    reject(new Error(jqXHR + "\n" + textStatus + "\n" + errorThrown));
                }
            }
        });
    });
}
function getReference(_this, position) {
    var segmentIndex = ~~(position / _this.segmentLength);
    if (segmentIndex >= _this.segments.length) {
        return undefined;
    }
    var segment = _this.segments[segmentIndex];
    if (!segment) {
        return undefined;
    }
    for (var _i = 0, _a = segment.references; _i < _a.length; _i++) {
        var symbolSpan = _a[_i];
        if (position >= symbolSpan.span.position &&
            position <= (symbolSpan.span.position + symbolSpan.span.length)) {
            return symbolSpan;
        }
    }
    return undefined;
}
function getDefinition(_this, position) {
    var segmentIndex = ~~(position / _this.segmentLength);
    if (segmentIndex >= _this.segments.length) {
        return undefined;
    }
    var segment = _this.segments[segmentIndex];
    if (!segment) {
        return undefined;
    }
    for (var _i = 0, _a = segment.definitions; _i < _a.length; _i++) {
        var symbolSpan = _a[_i];
        if (position >= symbolSpan.span.position &&
            position <= (symbolSpan.span.position + symbolSpan.span.length)) {
            return symbolSpan;
        }
    }
    return undefined;
}
function getSourceFileContents(projectId, filePath) {
    return undefined;
}
function getFindAllReferencesHtml(projectId, symbolId) {
    return null;
}
function getDefinitionLocation(projectId, symbol) {
    var url = "/definitionscontents/" + encodeURI(projectId) + "/?symbolId=" + encodeURI(symbol);
    return serverWithPrefix(url);
}
