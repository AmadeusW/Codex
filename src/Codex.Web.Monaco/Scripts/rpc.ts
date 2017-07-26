/// <reference path="../node_modules/@types/jquery/index.d.ts"/>

interface Span {
    position: number;
    length: number;
}

interface SymbolSpan {
    symbol: string;
    projectId: string;
    span: Span;
}

interface SegmentModel {
    definitions: SymbolSpan[];
    references: SymbolSpan[];
}

class SourceFileContentsModel {
    filePath: string;
    webLink: string;
    repoRelativePath: string;
    projectId: string;

    contents: string;
    span: Span;
    segments: SegmentModel[];
    // Width int characters of segments
    segmentLength: number;

    getReference(position: number): SymbolSpan {
        let segmentIndex = ~~(position / this.segmentLength);
        if (segmentIndex >= this.segments.length) {
            return undefined;
        }


        let segment = this.segments[segmentIndex];
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

    getDefinition(position: number): SymbolSpan {
        let segmentIndex = ~~(position / this.segmentLength);
        if (segmentIndex >= this.segments.length) {
            return undefined;
        }

        let segment = this.segments[segmentIndex];
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
}

interface DefinitionLocation {
    referencesHtml: string;
    sourceFile: SourceFileContentsModel;
}

function getSourceFileContents(projectId: string, filePath: string): SourceFileContentsModel {
    return undefined;
}

function getFindAllReferencesHtml(projectId: string, symbolId: string): string {
    return null;
}

function getDefinitionLocation(): Promise<DefinitionLocation> {
    return undefined;
}
