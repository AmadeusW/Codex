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

interface SourceFileContentsModel {
    contents: string;
    span: Span;
    definitions: SymbolSpan[];
    references: SymbolSpan[];
    vew: string;
}
