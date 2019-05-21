using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GoFish.DataAccess.VisualFoxPro.Search
{
    public class Searcher
    {
        private static Pool<StringBuilder> sbPool = new Pool<StringBuilder>(1, () => new StringBuilder(4096));

        private static readonly string templatePre = @"<!-- saved from url=(0014)about:internet -->
<!DOCTYPE html>
<html lang=""en"">

<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <meta http-equiv=""X-UA-Compatible"" content=""ie=edge"">
<style>code[class*=language-foxpro],pre[class*=language-foxpro]{tab-size:4;color:#000;font-family: 'Courier New',Consolas,Monaco,'Andale Mono','Ubuntu Mono',monospace;}
pre[class*=language-foxpro]{tab-size:4;padding:1em;margin:.5em 0;overflow:auto;border-radius:.3em}
.token.cdata,.token.comment,.token.doctype,.token.prolog{color:#008000}
.token.attr-name,.token.builtin,.token.char,.token.inserted,.token.selector,.token.string{color:#ff0000}
.token.atrule,.token.attr-value,.token.function{color:#0000ff}
.token.keyword{color:#0000ff}
.token.number{color:#FF00FF}
pre[class*=language-].line-numbers{position:relative;padding-left:3.8em;counter-reset:linenumber}pre[class*=language-].line-numbers>code{position:relative;white-space:inherit}.line-numbers .line-numbers-rows{position:absolute;pointer-events:none;top:0;font-size:100%;left:-3.8em;width:3em;letter-spacing:-1px;border-right:1px solid #999;-webkit-user-select:none;-moz-user-select:none;-ms-user-select:none;user-select:none}.line-numbers-rows>span{pointer-events:none;display:block;counter-increment:linenumber}.line-numbers-rows>span:before{content:counter(linenumber);color:#999;display:block;padding-right:.8em;text-align:right}
</style>
</head>
<body>
<pre class=""line-numbers""><code class=""language-foxpro"">";
        private readonly ISearchAlgorithm searchAlgorithm;
        private const string templatePost = @"</code></pre>
<script>
/* PrismJS 1.16.0
https://prismjs.com/download.html#themes=prism&plugins=keep-markup */
var _self=""undefined""!=typeof window?window:""undefined""!=typeof WorkerGlobalScope&&self instanceof WorkerGlobalScope?self:{},Prism=function(g){var c=/\blang(?:uage)?-([\w-]+)\b/i,a=0,C={manual:g.Prism&&g.Prism.manual,disableWorkerMessageHandler:g.Prism&&g.Prism.disableWorkerMessageHandler,util:{encode:function(e){return e instanceof M?new M(e.type,C.util.encode(e.content),e.alias):Array.isArray(e)?e.map(C.util.encode):e.replace(/&/g,""&amp;"").replace(/</g,""&lt;"").replace(/\u00a0/g,"" "")},type:function(e){return Object.prototype.toString.call(e).slice(8,-1)},objId:function(e){return e.__id||Object.defineProperty(e,""__id"",{value:++a}),e.__id},clone:function t(e,n){var r,a,i=C.util.type(e);switch(n=n||{},i){case""Object"":if(a=C.util.objId(e),n[a])return n[a];for(var l in r={},n[a]=r,e)e.hasOwnProperty(l)&&(r[l]=t(e[l],n));return r;case""Array"":return a=C.util.objId(e),n[a]?n[a]:(r=[],n[a]=r,e.forEach(function(e,a){r[a]=t(e,n)}),r);default:return e}}},languages:{extend:function(e,a){var t=C.util.clone(C.languages[e]);for(var n in a)t[n]=a[n];return t},insertBefore:function(t,e,a,n){var r=(n=n||C.languages)[t],i={};for(var l in r)if(r.hasOwnProperty(l)){if(l==e)for(var o in a)a.hasOwnProperty(o)&&(i[o]=a[o]);a.hasOwnProperty(l)||(i[l]=r[l])}var s=n[t];return n[t]=i,C.languages.DFS(C.languages,function(e,a){a===s&&e!=t&&(this[e]=i)}),i},DFS:function e(a,t,n,r){r=r||{};var i=C.util.objId;for(var l in a)if(a.hasOwnProperty(l)){t.call(a,l,a[l],n||l);var o=a[l],s=C.util.type(o);""Object""!==s||r[i(o)]?""Array""!==s||r[i(o)]||(r[i(o)]=!0,e(o,t,l,r)):(r[i(o)]=!0,e(o,t,null,r))}}},plugins:{},highlightAll:function(e,a){C.highlightAllUnder(document,e,a)},highlightAllUnder:function(e,a,t){var n={callback:t,selector:'code[class*=""language-""], [class*=""language-""] code, code[class*=""lang-""], [class*=""lang-""] code'};C.hooks.run(""before-highlightall"",n);for(var r,i=n.elements||e.querySelectorAll(n.selector),l=0;r=i[l++];)C.highlightElement(r,!0===a,n.callback)},highlightElement:function(e,a,t){for(var n,r,i=e;i&&!c.test(i.className);)i=i.parentNode;i&&(n=(i.className.match(c)||[,""""])[1].toLowerCase(),r=C.languages[n]),e.className=e.className.replace(c,"""").replace(/\s+/g,"" "")+"" language-""+n,e.parentNode&&(i=e.parentNode,/pre/i.test(i.nodeName)&&(i.className=i.className.replace(c,"""").replace(/\s+/g,"" "")+"" language-""+n));var l={element:e,language:n,grammar:r,code:e.textContent},o=function(e){l.highlightedCode=e,C.hooks.run(""before-insert"",l),l.element.innerHTML=l.highlightedCode,C.hooks.run(""after-highlight"",l),C.hooks.run(""complete"",l),t&&t.call(l.element)};if(C.hooks.run(""before-sanity-check"",l),l.code)if(C.hooks.run(""before-highlight"",l),l.grammar)if(a&&g.Worker){var s=new Worker(C.filename);s.onmessage=function(e){o(e.data)},s.postMessage(JSON.stringify({language:l.language,code:l.code,immediateClose:!0}))}else o(C.highlight(l.code,l.grammar,l.language));else o(C.util.encode(l.code));else C.hooks.run(""complete"",l)},highlight:function(e,a,t){var n={code:e,grammar:a,language:t};return C.hooks.run(""before-tokenize"",n),n.tokens=C.tokenize(n.code,n.grammar),C.hooks.run(""after-tokenize"",n),M.stringify(C.util.encode(n.tokens),n.language)},matchGrammar:function(e,a,t,n,r,i,l){for(var o in t)if(t.hasOwnProperty(o)&&t[o]){if(o==l)return;var s=t[o];s=""Array""===C.util.type(s)?s:[s];for(var g=0;g<s.length;++g){var c=s[g],u=c.inside,h=!!c.lookbehind,f=!!c.greedy,d=0,m=c.alias;if(f&&!c.pattern.global){var p=c.pattern.toString().match(/[imuy]*$/)[0];c.pattern=RegExp(c.pattern.source,p+""g"")}c=c.pattern||c;for(var y=n,v=r;y<a.length;v+=a[y].length,++y){var k=a[y];if(a.length>e.length)return;if(!(k instanceof M)){if(f&&y!=a.length-1){if(c.lastIndex=v,!(x=c.exec(e)))break;for(var b=x.index+(h?x[1].length:0),w=x.index+x[0].length,A=y,P=v,O=a.length;A<O&&(P<w||!a[A].type&&!a[A-1].greedy);++A)(P+=a[A].length)<=b&&(++y,v=P);if(a[y]instanceof M)continue;N=A-y,k=e.slice(v,P),x.index-=v}else{c.lastIndex=0;var x=c.exec(k),N=1}if(x){h&&(d=x[1]?x[1].length:0);w=(b=x.index+d)+(x=x[0].slice(d)).length;var j=k.slice(0,b),S=k.slice(w),E=[y,N];j&&(++y,v+=j.length,E.push(j));var _=new M(o,u?C.tokenize(x,u):x,m,x,f);if(E.push(_),S&&E.push(S),Array.prototype.splice.apply(a,E),1!=N&&C.matchGrammar(e,a,t,y,v,!0,o),i)break}else if(i)break}}}}},tokenize:function(e,a){var t=[e],n=a.rest;if(n){for(var r in n)a[r]=n[r];delete a.rest}return C.matchGrammar(e,t,a,0,0,!1),t},hooks:{all:{},add:function(e,a){var t=C.hooks.all;t[e]=t[e]||[],t[e].push(a)},run:function(e,a){var t=C.hooks.all[e];if(t&&t.length)for(var n,r=0;n=t[r++];)n(a)}},Token:M};function M(e,a,t,n,r){this.type=e,this.content=a,this.alias=t,this.length=0|(n||"""").length,this.greedy=!!r}if(g.Prism=C,M.stringify=function(a,t,e){if(""string""==typeof a)return a;if(Array.isArray(a))return a.map(function(e){return M.stringify(e,t,a)}).join("""");var n={type:a.type,content:M.stringify(a.content,t,e),tag:""span"",classes:[""token"",a.type],attributes:{},language:t,parent:e};if(a.alias){var r=Array.isArray(a.alias)?a.alias:[a.alias];Array.prototype.push.apply(n.classes,r)}C.hooks.run(""wrap"",n);var i=Object.keys(n.attributes).map(function(e){return e+'=""'+(n.attributes[e]||"""").replace(/""/g,""&quot;"")+'""'}).join("" "");return""<""+n.tag+' class=""'+n.classes.join("" "")+'""'+(i?"" ""+i:"""")+"">""+n.content+""</""+n.tag+"">""},!g.document)return g.addEventListener&&(C.disableWorkerMessageHandler||g.addEventListener(""message"",function(e){var a=JSON.parse(e.data),t=a.language,n=a.code,r=a.immediateClose;g.postMessage(C.highlight(n,C.languages[t],t)),r&&g.close()},!1)),C;var e=document.currentScript||[].slice.call(document.getElementsByTagName(""script"")).pop();return e&&(C.filename=e.src,C.manual||e.hasAttribute(""data-manual"")||(""loading""!==document.readyState?window.requestAnimationFrame?window.requestAnimationFrame(C.highlightAll):window.setTimeout(C.highlightAll,16):document.addEventListener(""DOMContentLoaded"",C.highlightAll))),C}(_self);""undefined""!=typeof module&&module.exports&&(module.exports=Prism),""undefined""!=typeof global&&(global.Prism=Prism);
!function(e,s){void 0!==e&&e.Prism&&e.document&&s.createRange&&(Prism.plugins.KeepMarkup=!0,Prism.hooks.add(""before-highlight"",function(e){if(e.element.children.length){var a=0,s=[],p=function(e,n){var o={};n||(o.clone=e.cloneNode(!1),o.posOpen=a,s.push(o));for(var t=0,d=e.childNodes.length;t<d;t++){var r=e.childNodes[t];1===r.nodeType?p(r):3===r.nodeType&&(a+=r.data.length)}n||(o.posClose=a)};p(e.element,!0),s&&s.length&&(e.keepMarkup=s)}}),Prism.hooks.add(""after-highlight"",function(n){if(n.keepMarkup&&n.keepMarkup.length){var a=function(e,n){for(var o=0,t=e.childNodes.length;o<t;o++){var d=e.childNodes[o];if(1===d.nodeType){if(!a(d,n))return!1}else 3===d.nodeType&&(!n.nodeStart&&n.pos+d.data.length>n.node.posOpen&&(n.nodeStart=d,n.nodeStartPos=n.node.posOpen-n.pos),n.nodeStart&&n.pos+d.data.length>=n.node.posClose&&(n.nodeEnd=d,n.nodeEndPos=n.node.posClose-n.pos),n.pos+=d.data.length);if(n.nodeStart&&n.nodeEnd){var r=s.createRange();return r.setStart(n.nodeStart,n.nodeStartPos),r.setEnd(n.nodeEnd,n.nodeEndPos),n.node.clone.appendChild(r.extractContents()),r.insertNode(n.node.clone),r.detach(),!1}}return!0};n.keepMarkup.forEach(function(e){a(n.element,{node:e,pos:0})}),n.highlightedCode=n.element.innerHTML}}))}(self,document);
!function(){if(""undefined""!=typeof self&&self.Prism&&self.document){var l=""line-numbers"",c=/\n(?!$)/g,m=function(e){var t=a(e)[""white-space""];if(""pre-wrap""===t||""pre-line""===t){var n=e.querySelector(""code""),r=e.querySelector("".line-numbers-rows""),s=e.querySelector("".line-numbers-sizer""),i=n.textContent.split(c);s||((s=document.createElement(""span"")).className=""line-numbers-sizer"",n.appendChild(s)),s.style.display=""block"",i.forEach(function(e,t){s.textContent=e||""\n"";var n=s.getBoundingClientRect().height;r.children[t].style.height=n+""px""}),s.textContent="""",s.style.display=""none""}},a=function(e){return e?window.getComputedStyle?getComputedStyle(e):e.currentStyle||null:null};window.addEventListener(""resize"",function(){Array.prototype.forEach.call(document.querySelectorAll(""pre.""+l),m)}),Prism.hooks.add(""complete"",function(e){if(e.code){var t=e.element,n=t.parentNode;if(n&&/pre/i.test(n.nodeName)&&!t.querySelector("".line-numbers-rows"")){for(var r=!1,s=/(?:^|\s)line-numbers(?:\s|$)/,i=t;i;i=i.parentNode)if(s.test(i.className)){r=!0;break}if(r){t.className=t.className.replace(s,"" ""),s.test(n.className)||(n.className+="" line-numbers"");var l,a=e.code.match(c),o=a?a.length+1:1,u=new Array(o+1).join(""<span></span>"");(l=document.createElement(""span"")).setAttribute(""aria-hidden"",""true""),l.className=""line-numbers-rows"",l.innerHTML=u,n.hasAttribute(""data-start"")&&(n.style.counterReset=""linenumber ""+(parseInt(n.getAttribute(""data-start""),10)-1)),e.element.appendChild(l),m(n),Prism.hooks.run(""line-numbers"",e)}}}}),Prism.hooks.add(""line-numbers"",function(e){e.plugins=e.plugins||{},e.plugins.lineNumbers=!0}),Prism.plugins.lineNumbers={getLine:function(e,t){if(""PRE""===e.tagName&&e.classList.contains(l)){var n=e.querySelector("".line-numbers-rows""),r=parseInt(e.getAttribute(""data-start""),10)||1,s=r+(n.children.length-1);t<r&&(t=r),s<t&&(t=s);var i=t-r;return n.children[i]}}}}}();</script>
<script>Prism.languages.foxpro={
            comment:/(?:&&|^(?: |\t)*\*).*/m,
            string:/(?:""(?:""""|.)*?""|\[(?:""""|.)*?\]|'(?:""""|.)*?')/i,
            keyword:/\b(?:_SCREEN|ACTIVATE (?:MENU|POPUP|SCREEN|WINDOW)|ADD|ADDITEM|AND|ALWAYSONTOP|AS|BUILD (?:PROJECT|DLL)|CALL(?: ABSOLUTE)?|APPEND(?: BLANK)?|_CLIPTEXT|CASE|CATCH|CHDIR|CLEAR|CLOSE|CLS|COM|CONTROL|CONTROLSOURCE|COPY (?:FILE|INDEXES|MEMO|PROCEDURES|STRUCTURE(?: EXTENDED)|TAG|TO)|DECLARE|DEFINE|DIMENSION|DELETE (?:ALL|CONNECTION|DATABASE|FILE|FOR|FROM|IN|NEXT|REST|TAG|TRIGGER ON|VIEW)|DO(?: CASE| WHILE)?|ELSE|ENABLED|END(?:CASE|DO|FOR|FUNC|IF|PROC|SCAN|TEXT|TRY|WITH)|ERASE|EXCEPTION|EXIT|EXTERNAL|FIELD|FINALLY|FOR|FROM|FUNCTION|GETDATA|GETFORMAT|GO|GOTO|HIDE|IF|INTEGER|ITEM|KEY|KEYBOARD|LOCAL|LOCATE|LOCKSCREEN|LOOP|LPARAMETERS|METHOD|MKDIR|MODIFY (?:CLASS|COMMAND|CONNECTION|DATABASE|FILE|FORM|GENERAL|LABEL|MEMO|MENU|PROCEDURE|PROJECT|QUERY|REPORT|STRUCTURE|VIEW|WINDOW)|NAME|NEXT|NOCLEAR|NODE|NOT|NOWAIT|OR|OF|OFF|ON(?: BAR| ERROR| ESCAPE| KEY LABEL| PAD| PAGE| SELECTION (?:BAR|MENU|PAD|POPUP)|SHUTDOWN)?|OPEN|OTHERWISE|PARAMETERS|PARENT|PICTURE|PROCEDURE|READ|RECOMPILE|RELATIVE|RELEASE|RESUME|RETURN|REPLACE|RMDIR|RUN|SCAN|SINGLE|SELECT CASE|SHELL|STATIC|SET(?: (?:ALTERNATE (?:ON|OFF|TO )|ANSI (?:ON|OFF)|ASSERTS|AUTOINCERROR|AUTOSAVE|BELL|BLOCKSIZE TO|BROWSEIME|CARRY|CENTURY|CLASSLIB TO|CLOCK|COLLATE TO|COLOR|COMPATIBLE|CONFIRM|CONSOLE|COVERAGE TO|CPCOMPILE TO|CPDIALOG|CURRENCY TO|CURSOR|DATABASE TO|DATASESSION TO|DATE|DEBUG|DEBUGOUT TO|DECIMALS TO|DEFAULT TO|DELETED|DEVELOPMENT|DEVICE TO|DISPLAY TO|DOHISTORY|ECHO|ENGINEBEHAVIOR|ESCAPE|EVENTLIST TO|EVENTTRACKING|EXACT|EXCLUSIVE|FDOW TO|FIELDS|FILTER TO|FIXED|FULLPATH|FUNCTION|FWEEK TO|HEADINGS|HELP|HELPFILTER|HOURS TO|INDEX TO|KEY TO|KEYCOMP TO|LIBRARY TO|LOCK|LOGERRORS|MACKEY TO|MARGIN TO|MARK|MEMOWIDTH TO|MESSAGE|MULTILOCKS|NEAR|NOCPTRANS TO|NOTIFY|NULL|NULLDISPLAY TO|ODOMETER TO|OLEOBJECT|OPTIMIZE|ORDER TO|PALETTE|PATH TO|PDSETUP TO|POINT TO|PRINTER|PROCEDURE TO|READBORDER|REFRESH TO|RELATION|REPORTBEHAVIOR|REPROCESS TO|RESOURCE|SAFETY|SECONDS|SEPARATOR|SKIP|SPACE|SQLBUFFERING|STATUS|STATUS BAR|STEP ON|STRICTDATE TO|SYSFORMATS|SYSMENU|TABLEPROMPT|TABLEVALIDATE TO|TALK|TEXTMERGE|TOPIC|TRBETWEEN|TYPEAHEAD TO|UDFPARAMS TO|UNIQUE|VARCHARMAPPING|VIEW)?)|SHORTCUT|STORE|STOP|STRING|TEXT(?: TO)|THEN|THIS|THISFORM|THISFORMSET|THROW|TIMER|TO|TOP|TRY|TYPE|PRINT|USE(?: IN)?|USERVALUE|VALUE|VISIBLE|WAIT|WHEN|WINDOW|WITH|ZAP)(?:\$|\b)/i,
            ""function"":/\b(?:ABS|ACLASS|ACOPY|ACOS|ADATABASES|ADBOJECTS|ADBS|ADDCLASS|ADDCOLUMN|ADDFILE|ADDIN|ADDINMENU|ADDINMETHOD|ADDPROPERTY|ADEL|ADIR|ADLLS|ADOCKSTATE|AERROR|AEVENTS|AFIELDS|AFONT|AGETCLASS|AGETFILEVERSION|AINS|AINSTANCE|ALANGUAGE|ALIAS|ALINES|ALLTRIM|ALEN|AMEMBERS|AMOUSEOBJ|ANETRESOURCES|ANSITOOEM|APRINTERS|APROCINFO|ASC|ASCAN|ASESSIONS|ASIN|ASORT|ASQLHANDLES|ARRAY|AT|AT_C|ATAGINFO|ATAN|ATC|ATCC|ATCLINE|ATLINE|ATN2|AUSED|BAR|PARPROMPT|BETWEEN|BINDEVENT|BITAND|BITCLEAR|BITLSHIFT|BITNOT|BITOR|BITRSHIFT|BITSET|BITTEST|BITXOR|BOF|CANDIDATE|CAPSLOCK|CDOW|CDX|CEILING|CLEARRESULTSET|CNTBAR|CNTPAD|COMARRAY|COMCLASSINFO|COMPOBJ|COMPROP|COMRETURNERROR|COS|CPCONVERT|CPCURRENT|CPDBF|CREATEOBJECT|CREATEOBJECTEX|CREATEOFFLINE|CREATE (?:CLASS|CLASSLIB|COLOR SET|CONNECTION|CURSOR|DATABASE|FORM|LABEL|MENU|PROJECT|QUERY|REPORT|SQL VIEW|TABLE|TRIGGER ON|VIEW)|CTOBIN|CTOT|CURDIR|CURSORGETPROP|CURSORTOXML|CURVAL|CHR|CHRTRAN|COLLATE|COLOR|DATETIME|DAY|DBGETPROP|DBSETPROP|DBUSED|DDEABORTTRANS|DDEADVISE|DDEENABLED|DDEINITIATE|DDELASTERROR|DDEPOKE|DDEREQUEST|DDESETOPTION|DDESETSERVICE|DDESETTOPIC|DDETERMINATE|DEFAULTTEXT|DELETED|DESCENDING|DIFFERENCE|DIRECTORY|DISKSPACE|DMY|DODEFAULT|DOW|DRIVETYPE|DROPOFFLINE|DTOC|DTOR|DTOS|DTOT|EDITSOURCE|EMPTY|EOF|ERROR|EVALUATE|EVENTHANDLER|EVL|EXECSCRIPT|EXP|FCHSIZE|FCLOSE|FCOUNT|FCREATE|FDATE|FEOF|FERROR|FFLUSH|FGETS|FIELD|FILE|FILETOSTR|FILTER|FKLABEL|FLDLIST|FLOCK|FLOOR|FONTMETRIC|FOR|FORCEEXT|FOUND|FPUTS|FREAD|FSEEK|FSIZE|FTIME|FULLPATH|FV|FWRITE|GETAUTOINCVALUE|GETBAR|GETCOLOR|GETCP|GETCURSORADAPTER|GETDIR|GETFILE|GETFLDSTATE|GETINTERFACE|GETNEXTMODIFIED|GETOBJECT|GETPAD|GETPEM|GETPICT|GETPRINTER|GETRESULTSET|GETWORDCOUNT|GETWORDNUM|GOMONTH|HEADER|HOME|HOUR|ICASE|ID|IDXCOLLATE|IIF|IMESTATUS|INKEY|INLIST|INPUTBOX|INSMODE|INT|ISALPHA|ISBLANK|ISCOLOR|ISDIGIT|ISFLOCKED|ISLEADBYTE|ISLOWER|ISMEMOFETCHED|ISMOUSE|ISNULL|ISPEN|ISREADONLY|ISRLOCKED|ISTRANSACTABLE|ISUPPER|JUSTDRIVE|JUSTEXT|JUSTFNAME|JUSTPATH|JUSTSTEM|KEY|KEYMATCH|LASTKEY|LEFT|LEFTC|LEN|LENC|LIKE|LIKEC|LINENO|LOADPICTURE|LOCFILE|LOCK|LOG|LOOKUP|LOWER|LTRIM|LUPDATE|MAKETRANSACTABLE|MAX|MDX|MDY|MEMORY|MENU|MESSAGE|MESSAGEBOX|MIN|MINUTE|MLINE|MOD|MONTH|MRKBAR|MRKPAD|MTON|MWINDOW|NDX|NORMALIZE|NTOM|NUMLOCK|NVL|OBJNUM|OBJTOCLIENT|OBJVAR|OCCURS|OEMTOANSI|OLDVAL|ORDER|OS|PAD|PADC|PADL|PADR|PARAMETERS|PAYMENT|PCOL|PCOUNT|PEMSTATUS|PI|POPUP|PRIMARY|PRINTSTATUS|PRMBAR|PRMPAD|PROGRAM|PROMPT|PROPER|PROW|PUTFILE|QUARTER|RAISEEVENT|RAND|RAT|RATC|RATLINE|RDLEVEL|READKEY|RECCOUNT|RECNO|RECSIZE|REFRESH|REMOVEPROPERTY|REPLICATE|REQUERY|RGB|RGBSCHEME|RIGHT|RIGHTC|RLOCK|ROW|RTOD|RTRIM|SAVEPICTURE|SCHEME|SCOLS|SEC|SECONDS|SEEK|SELECT|SET|SETFLDSTATE|SIGN|SIN|SKPBAR|SKPPAD|SOUNDEX|SPACE|SQLCANCEL|SQLCOMMIT|SQLCONNECT|SQLDISCONNECT|SQLEXEC|SQLGETPROP|SQLIDLEDISCONNECT|SQLMORERESULTS|SQLPREPARE|SQLROLLBACK|SQLSTRINGCONNECT|SQLTABLES|SQRT|SROWS|STR|STRCONV|STREXTRACT|STRTOFILE|STRTRAN|STUFF|SUBSTR|SUBSTRC|SYS|TABLEREVERT|TABLEUPDATE|TAG|TAGCOUNT|TAGNO|TAN|TARGET|TEXTMERGE|TIME|TRANSFORM|TTOC|TTOD|TXTWIDTH|UNBINDEVENTS|UNIQUE|UPDATED|UPPER|USED|VAL|VARTYPE|VERSION|WCHILD|WCOLS|WDOCKABLE|WEEK|WEXIST|WFONT|WLAST|WLCOL|WMAXIMUM|WMINIMUM|WOUTPUT|WROWS|YEAR)(?:\$|\b)/i,
            number:/(?:\b(?:\d+(?:\.|x|h)?\d*|\B\.\d+|\.T\.|\.F\.|NULL|\.NULL\.)(?:\$|\b))(?:E[+-]?\d+)?/i};
</script>
</body>";

        public Searcher(ISearchAlgorithm searchAlgorithm)
        {
            this.searchAlgorithm = searchAlgorithm;
        }

        public IEnumerable<SearchResult> Search(ClassLibrary lib, string text, bool ignoreCase = false)
        {
            return this.searchAlgorithm.Search(lib, text, ignoreCase);
        }

        /// <summary>
        /// Saves the results as HTML5 files in "<paramref name="directoryPath"/>/[ClassLibrary/]Class.Method.html"
        /// </summary>
        /// <param name="searchResults"></param>
        /// <param name="directoryPath"></param>
        public void SaveResults(IEnumerable<SearchResult> searchResults, Func<SearchResult, string> fileNameTemplate, string directoryPath)
        {
            foreach (SearchResult r in searchResults)
            {
                DirectoryInfo dirPath = Directory.CreateDirectory(Path.Combine(directoryPath, r.Library));

                HTMLifySurroundLinesToFile(r.Content,
                    Path.Combine(directoryPath, r.Library, $"{fileNameTemplate(r)}.html"), r.Line, "<mark>", "</mark>");
            }
        }

        private static void HTMLifySurroundLines<TWriter>(
            TWriter writer,
            Action<TWriter, string> writeLine,
            Action<TWriter, string> write,
            string source,
            int line,
            string prefix,
            string suffix = null,
            int tabSize = 4)
        {
            using (StringReader sr = new StringReader(source))
            {
                write(writer, templatePre);
                int lineNo = 0;
                while (sr.ReadLine() is string l)
                {
                    int lineLen = l.Length;
                    l = l.TrimStart();
                    if (l.Length < lineLen)
                    {
                        l = new string(' ', (lineLen - l.Length) * tabSize) + l;
                    }

                    if (line == lineNo)
                    {
                        write(writer, prefix);
                        write(writer, l.Replace("<", "&lt;").Replace(">", "&gt;"));
                        writeLine(writer, suffix ?? prefix);
                    }
                    else
                    {
                        writeLine(writer, l.Replace("<", "&lt;").Replace(">", "&gt;"));
                    }
                    lineNo++;
                }
                write(writer, templatePost);
            }
        }

        private static void HTMLifySurroundLinesToFile(string source, string filePath, int line, string prefix, string suffix = null, int tabSize = 4)
        {
            using (StreamWriter sw = new StreamWriter(filePath, false, Encoding.UTF8))
            {
                HTMLifySurroundLines(sw, (w, v) => w.WriteLine(v), (w, v) => w.Write(v), source, line, prefix, suffix, tabSize);
            }
        }

        public static string HTMLifySurroundLines(string source, int line, string prefix, string suffix = null, int tabSize = 4)
        {
            StringBuilder sb = sbPool.Rent();
            HTMLifySurroundLines(sb, (w, v) => w.AppendLine(v), (w, v) => w.Append(v), source, line, prefix, suffix, tabSize);
            string result = sb.ToString();
            sb.Clear();
            sbPool.Return(sb);
            return result;
        }
    }
}
