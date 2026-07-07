"use client";

function renderInline(text: string) {
  return text.split(/(`[^`]+`|\*\*[^*]+\*\*)/g).map((part, i) => {
    if (part.startsWith("`") && part.endsWith("`")) {
      return <code key={i} className="px-1 py-0.5 rounded bg-zinc-800 text-indigo-300 text-sm">{part.slice(1, -1)}</code>;
    }
    if (part.startsWith("**") && part.endsWith("**")) {
      return <strong key={i} className="text-zinc-100 font-semibold">{part.slice(2, -2)}</strong>;
    }
    return part;
  });
}

export function MarkdownViewer({ content }: { content: string }) {
  const lines = content.split("\n");
  const elements: React.ReactNode[] = [];
  let inCode = false;
  let codeLines: string[] = [];
  let listItems: string[] = [];

  const flushList = () => {
    if (listItems.length === 0) return;
    elements.push(
      <ul key={`list-${elements.length}`} className="list-disc list-inside space-y-1 text-zinc-300 my-3">
        {listItems.map((item, i) => <li key={i}>{renderInline(item)}</li>)}
      </ul>
    );
    listItems = [];
  };

  for (let i = 0; i < lines.length; i++) {
    const line = lines[i];

    if (line.startsWith("```")) {
      flushList();
      if (!inCode) {
        inCode = true;
        codeLines = [];
      } else {
        elements.push(
          <pre key={`code-${i}`} className="my-4 p-4 rounded-lg bg-zinc-950 border border-zinc-800 overflow-x-auto text-sm text-zinc-300 font-mono">
            {codeLines.join("\n")}
          </pre>
        );
        inCode = false;
      }
      continue;
    }

    if (inCode) {
      codeLines.push(line);
      continue;
    }

    if (line.startsWith("# ")) {
      flushList();
      elements.push(<h1 key={i} className="text-2xl font-bold text-zinc-100 mt-6 mb-3">{line.slice(2)}</h1>);
    } else if (line.startsWith("## ")) {
      flushList();
      elements.push(<h2 key={i} className="text-xl font-semibold text-zinc-100 mt-5 mb-2">{line.slice(3)}</h2>);
    } else if (line.startsWith("### ")) {
      flushList();
      elements.push(<h3 key={i} className="text-lg font-medium text-zinc-200 mt-4 mb-2">{line.slice(4)}</h3>);
    } else if (line.match(/^[-*] /)) {
      listItems.push(line.slice(2));
    } else if (line.trim() === "") {
      flushList();
    } else {
      flushList();
      elements.push(<p key={i} className="text-zinc-300 my-2 leading-relaxed">{renderInline(line)}</p>);
    }
  }

  flushList();

  return <article className="prose-invert max-w-none">{elements}</article>;
}
