/// <reference path="../node_modules/@types/jquery/index.d.ts"/>
var SourceFileContentsModel = (function () {
    function SourceFileContentsModel() {
    }
    SourceFileContentsModel.prototype.getReference = function (position) {
        var segmentIndex = ~~(position / this.segmentLength);
        if (segmentIndex >= this.segments.length) {
            return undefined;
        }
        var segment = this.segments[segmentIndex];
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
    };
    SourceFileContentsModel.prototype.getDefinition = function (position) {
        var segmentIndex = ~~(position / this.segmentLength);
        if (segmentIndex >= this.segments.length) {
            return undefined;
        }
        var segment = this.segments[segmentIndex];
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
    };
    return SourceFileContentsModel;
}());
function getSourceFileContents(projectId, filePath) {
    return undefined;
}
function getFindAllReferencesHtml(projectId, symbolId) {
    return null;
}
function getDefinitionLocation() {
    return undefined;
}
