"use strict";

const META_FIELD_COUNT = 21;
const META_COLUMNS = Array.from(
  { length: META_FIELD_COUNT },
  (_, index) => `meta${index + 1}`,
);

const BASE_COLUMNS = [
  "schema_version",
  "raw_record_id",
  "session_id",
  "first_seen_at",
  "source_file",
  "window_id",
  "rotation_index",
  "file_mtime",
  "file_size",
  "file_hash",
  "record_index",
  "record_offset",
  "raw_record_hash",
  ...META_COLUMNS,
  "event_group",
  "sequence_hint",
  "message_token_count",
  "display.color_code",
  "raw_message_hex",
  "visible_text",
  "message_time_text",
  "message_time_precision",
  "is_marker",
  "marker_keyword",
  "parse_status",
  "parse_error",
];

const state = {
  records: [],
  columns: [...BASE_COLUMNS],
  filteredIndexes: [],
  columnFilters: new Map(),
  page: 0,
  pageSize: 250,
  selected: null,
  sourceFileName: "",
};

const elements = {
  dropZone: document.getElementById("dropZone"),
  fileInput: document.getElementById("fileInput"),
  tableTabButton: document.getElementById("tableTabButton"),
  replayTabButton: document.getElementById("replayTabButton"),
  tableTabPanel: document.getElementById("tableTabPanel"),
  replayTabPanel: document.getElementById("replayTabPanel"),
  pageSizeSelect: document.getElementById("pageSizeSelect"),
  prevPageButton: document.getElementById("prevPageButton"),
  nextPageButton: document.getElementById("nextPageButton"),
  pageInfo: document.getElementById("pageInfo"),
  statusText: document.getElementById("statusText"),
  copyStatusText: document.getElementById("copyStatusText"),
  copySelectedButton: document.getElementById("copySelectedButton"),
  exportCsvButton: document.getElementById("exportCsvButton"),
  clearButton: document.getElementById("clearButton"),
  tableHead: document.getElementById("tableHead"),
  tableBody: document.getElementById("tableBody"),
  window1Log: document.getElementById("window1Log"),
  window2Log: document.getElementById("window2Log"),
};

elements.fileInput.addEventListener("change", () => {
  const file = elements.fileInput.files?.[0];
  if (file) {
    void loadFile(file);
  }
});

elements.dropZone.addEventListener("dragover", (event) => {
  event.preventDefault();
  elements.dropZone.classList.add("drag-over");
});

elements.dropZone.addEventListener("dragleave", () => {
  elements.dropZone.classList.remove("drag-over");
});

elements.dropZone.addEventListener("drop", (event) => {
  event.preventDefault();
  elements.dropZone.classList.remove("drag-over");
  const file = event.dataTransfer?.files?.[0];
  if (file) {
    void loadFile(file);
  }
});

elements.tableTabButton.addEventListener("click", () => {
  activateTab("table");
});

elements.replayTabButton.addEventListener("click", () => {
  activateTab("replay");
});

elements.pageSizeSelect.addEventListener("change", () => {
  state.pageSize = Number(elements.pageSizeSelect.value);
  state.page = 0;
  renderTableBody();
});

elements.prevPageButton.addEventListener("click", () => {
  if (state.page > 0) {
    state.page -= 1;
    renderTableBody();
  }
});

elements.nextPageButton.addEventListener("click", () => {
  const lastPage = getLastPage();
  if (state.page < lastPage) {
    state.page += 1;
    renderTableBody();
  }
});

elements.copySelectedButton.addEventListener("click", () => {
  void copySelectedCells();
});

elements.exportCsvButton.addEventListener("click", () => {
  exportCsv();
});

elements.clearButton.addEventListener("click", () => {
  clearAll();
});

document.addEventListener("keydown", (event) => {
  if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "c") {
    if (state.selected) {
      event.preventDefault();
      void copySelectedCells();
    }
  }
});

async function loadFile(file) {
  setStatus(`読込中: ${file.name}`);
  setCopyStatus("");

  try {
    const text = await file.text();
    const result = parseJsonl(text);
    state.records = result.records;
    state.columns = buildColumns(result.records);
    state.columnFilters = new Map();
    state.page = 0;
    state.selected = null;
    state.sourceFileName = file.name;
    elements.clearButton.disabled = false;
    elements.copySelectedButton.disabled = true;
    elements.exportCsvButton.disabled = result.records.length === 0;

    renderTableHead();
    applyFiltersAndRender();
    renderLogReplay();

    const errorMessage = result.errors.length
      ? ` / 解析エラー ${result.errors.length} 行`
      : "";
    setStatus(`${file.name}: ${result.records.length} 件${errorMessage}`);
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    setStatus(`読込失敗: ${message}`);
  }
}

function parseJsonl(text) {
  const records = [];
  const errors = [];
  const lines = text.replace(/^\uFEFF/, "").split(/\r?\n/);

  lines.forEach((line, index) => {
    if (!line.trim()) {
      return;
    }

    try {
      const source = JSON.parse(line);
      records.push(flattenRecord(source, index + 1));
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      errors.push({ lineNumber: index + 1, message });
      records.push(createParseErrorRecord(index + 1, line, message));
    }
  });

  return { records, errors };
}

function flattenRecord(source, lineNumber) {
  const record = {};

  for (const [key, value] of Object.entries(source)) {
    if (key === "meta_fields") {
      continue;
    }

    if (key === "display" && value && typeof value === "object" && !Array.isArray(value)) {
      for (const [displayKey, displayValue] of Object.entries(value)) {
        record[`display.${displayKey}`] = stringifyValue(`display.${displayKey}`, displayValue);
      }
      continue;
    }

    record[key] = stringifyValue(key, value);
  }

  const metaFields = Array.isArray(source.meta_fields) ? source.meta_fields : [];
  META_COLUMNS.forEach((column, index) => {
    record[column] = stringifyValue(column, metaFields[index] ?? "");
  });

  record.__line_number = String(lineNumber);
  return record;
}

function createParseErrorRecord(lineNumber, line, message) {
  const record = {
    __line_number: String(lineNumber),
    parse_status: "error",
    parse_error: `JSONL解析に失敗しました: ${message}`,
    raw_line: line,
  };

  META_COLUMNS.forEach((column) => {
    record[column] = "";
  });

  return record;
}

function stringifyValue(key, value) {
  if (value === null || value === undefined) {
    return "";
  }

  if (key === "visible_text" && typeof value === "string") {
    return decodeUnicodeEscapes(value);
  }

  if (typeof value === "string") {
    return value;
  }

  if (typeof value === "number" || typeof value === "boolean") {
    return String(value);
  }

  return JSON.stringify(value);
}

function decodeUnicodeEscapes(value) {
  if (!/\\u[0-9a-fA-F]{4}/.test(value)) {
    return value;
  }

  return value
    .replace(
      /\\u(d[89ab][0-9a-fA-F]{2})\\u(d[cdef][0-9a-fA-F]{2})/gi,
      (_, high, low) => {
        const highCode = Number.parseInt(high, 16);
        const lowCode = Number.parseInt(low, 16);
        const codePoint =
          0x10000 + ((highCode - 0xd800) << 10) + (lowCode - 0xdc00);
        return String.fromCodePoint(codePoint);
      },
    )
    .replace(/\\u([0-9a-fA-F]{4})/g, (_, hex) =>
      String.fromCharCode(Number.parseInt(hex, 16)),
    );
}

function buildColumns(records) {
  const seen = new Set(["__line_number", ...BASE_COLUMNS]);
  const extraColumns = [];

  for (const record of records) {
    for (const key of Object.keys(record)) {
      if (!seen.has(key)) {
        seen.add(key);
        extraColumns.push(key);
      }
    }
  }

  return ["__line_number", ...BASE_COLUMNS, ...extraColumns];
}

function renderTableHead() {
  elements.tableHead.textContent = "";

  const headerRow = document.createElement("tr");
  const filterRow = document.createElement("tr");
  filterRow.className = "filter-row";

  for (const column of state.columns) {
    const headerCell = document.createElement("th");
    headerCell.textContent = column;
    headerCell.title = column;
    headerRow.appendChild(headerCell);

    const filterCell = document.createElement("th");
    const input = document.createElement("input");
    input.type = "search";
    input.placeholder = "フィルター";
    input.dataset.column = column;
    input.addEventListener("input", () => {
      const value = normalizeFilter(input.value);
      if (value) {
        state.columnFilters.set(column, value);
      } else {
        state.columnFilters.delete(column);
      }
      state.page = 0;
      applyFiltersAndRender();
    });
    filterCell.appendChild(input);
    filterRow.appendChild(filterCell);
  }

  elements.tableHead.append(headerRow, filterRow);
}

function applyFiltersAndRender() {
  state.filteredIndexes = [];

  state.records.forEach((record, index) => {
    if (matchesRecord(record)) {
      state.filteredIndexes.push(index);
    }
  });

  state.selected = null;
  elements.copySelectedButton.disabled = true;
  renderTableBody();
}

function matchesRecord(record) {
  for (const [column, filter] of state.columnFilters.entries()) {
    if (!normalizeFilter(record[column] ?? "").includes(filter)) {
      return false;
    }
  }

  return true;
}

function renderTableBody() {
  elements.tableBody.textContent = "";

  const total = state.filteredIndexes.length;
  const lastPage = getLastPage();
  if (state.page > lastPage) {
    state.page = lastPage;
  }

  const start = state.page * state.pageSize;
  const end = Math.min(start + state.pageSize, total);

  for (let rowIndex = start; rowIndex < end; rowIndex += 1) {
    const recordIndex = state.filteredIndexes[rowIndex];
    const record = state.records[recordIndex];
    const row = document.createElement("tr");

    state.columns.forEach((column, columnIndex) => {
      const cell = document.createElement("td");
      const value = record[column] ?? "";
      cell.textContent = value;
      cell.title = value;
      cell.dataset.row = String(rowIndex);
      cell.dataset.column = String(columnIndex);

      if (column === "parse_error" && value) {
        cell.classList.add("error-cell");
      }

      cell.addEventListener("click", (event) => {
        selectCellRange(rowIndex, columnIndex, event.shiftKey);
      });

      cell.addEventListener("dblclick", () => {
        state.selected = {
          startRow: rowIndex,
          endRow: rowIndex,
          startColumn: columnIndex,
          endColumn: columnIndex,
        };
        paintSelection();
        void copySelectedCells();
      });

      row.appendChild(cell);
    });

    elements.tableBody.appendChild(row);
  }

  updatePager(total);
  paintSelection();
}

function selectCellRange(rowIndex, columnIndex, expand) {
  if (expand && state.selected) {
    state.selected.endRow = rowIndex;
    state.selected.endColumn = columnIndex;
  } else {
    state.selected = {
      startRow: rowIndex,
      endRow: rowIndex,
      startColumn: columnIndex,
      endColumn: columnIndex,
    };
  }

  elements.copySelectedButton.disabled = false;
  paintSelection();
  setCopyStatus("選択範囲は Ctrl+C でコピーできます");
}

function paintSelection() {
  const selected = state.selected;
  const cells = elements.tableBody.querySelectorAll("td");
  cells.forEach((cell) => cell.classList.remove("selected"));

  if (!selected) {
    return;
  }

  const rowStart = Math.min(selected.startRow, selected.endRow);
  const rowEnd = Math.max(selected.startRow, selected.endRow);
  const columnStart = Math.min(selected.startColumn, selected.endColumn);
  const columnEnd = Math.max(selected.startColumn, selected.endColumn);

  cells.forEach((cell) => {
    const row = Number(cell.dataset.row);
    const column = Number(cell.dataset.column);
    if (
      row >= rowStart &&
      row <= rowEnd &&
      column >= columnStart &&
      column <= columnEnd
    ) {
      cell.classList.add("selected");
    }
  });
}

function activateTab(tabName) {
  const isTableTab = tabName === "table";
  elements.tableTabButton.classList.toggle("active", isTableTab);
  elements.replayTabButton.classList.toggle("active", !isTableTab);
  elements.tableTabButton.setAttribute("aria-selected", String(isTableTab));
  elements.replayTabButton.setAttribute("aria-selected", String(!isTableTab));
  elements.tableTabPanel.hidden = !isTableTab;
  elements.replayTabPanel.hidden = isTableTab;
  elements.tableTabPanel.classList.toggle("active", isTableTab);
  elements.replayTabPanel.classList.toggle("active", !isTableTab);
}

function renderLogReplay() {
  elements.window1Log.textContent = "";
  elements.window2Log.textContent = "";

  for (const record of state.records) {
    const windowId = record.window_id;
    if (windowId !== "1" && windowId !== "2") {
      continue;
    }

    const line = document.createElement("div");
    const visibleText = record.visible_text || "\u00A0";
    line.className = "log-line";
    line.textContent = visibleText;
    line.style.color = toCssColor(record["display.color_code"]);
    line.title = [
      `line=${record.__line_number ?? ""}`,
      `record_index=${record.record_index ?? ""}`,
      `color_code=${record["display.color_code"] ?? ""}`,
    ].join(" / ");

    if (windowId === "1") {
      elements.window1Log.appendChild(line);
    } else {
      elements.window2Log.appendChild(line);
    }
  }
}

function toCssColor(colorCode) {
  const normalized = String(colorCode ?? "").trim();
  if (/^[0-9a-fA-F]{8}$/.test(normalized)) {
    return `#${normalized.slice(2)}`;
  }

  if (/^[0-9a-fA-F]{6}$/.test(normalized)) {
    return `#${normalized}`;
  }

  return "inherit";
}

async function copySelectedCells() {
  if (!state.selected) {
    return;
  }

  const selectedText = buildSelectedTsv(state.selected);
  await copyText(selectedText);
  const cellCount = selectedText ? selectedText.split(/\t|\n/).length : 0;
  setCopyStatus(`${cellCount} セルをコピーしました`);
}

function buildSelectedTsv(selected) {
  const rowStart = Math.min(selected.startRow, selected.endRow);
  const rowEnd = Math.max(selected.startRow, selected.endRow);
  const columnStart = Math.min(selected.startColumn, selected.endColumn);
  const columnEnd = Math.max(selected.startColumn, selected.endColumn);
  const lines = [];

  for (let rowIndex = rowStart; rowIndex <= rowEnd; rowIndex += 1) {
    const recordIndex = state.filteredIndexes[rowIndex];
    const record = state.records[recordIndex];
    const values = [];

    for (let columnIndex = columnStart; columnIndex <= columnEnd; columnIndex += 1) {
      const column = state.columns[columnIndex];
      values.push(toTsvCell(record[column] ?? ""));
    }

    lines.push(values.join("\t"));
  }

  return lines.join("\n");
}

async function copyText(text) {
  if (navigator.clipboard?.writeText) {
    await navigator.clipboard.writeText(text);
    return;
  }

  const textarea = document.createElement("textarea");
  textarea.value = text;
  textarea.style.position = "fixed";
  textarea.style.left = "-9999px";
  document.body.appendChild(textarea);
  textarea.select();
  document.execCommand("copy");
  textarea.remove();
}

function toTsvCell(value) {
  return String(value).replace(/\r?\n/g, " ");
}

function exportCsv() {
  if (!state.records.length) {
    setCopyStatus("CSV出力対象のデータがありません");
    return;
  }

  const csv = buildCsv();
  const blob = new Blob([`\uFEFF${csv}`], {
    type: "text/csv;charset=utf-8",
  });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = buildCsvFileName();
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
  setCopyStatus(`${state.filteredIndexes.length} 件をCSV出力しました`);
}

function buildCsv() {
  const lines = [
    state.columns.map(toCsvCell).join(","),
  ];

  for (const recordIndex of state.filteredIndexes) {
    const record = state.records[recordIndex];
    const values = state.columns.map((column) => toCsvCell(record[column] ?? ""));
    lines.push(values.join(","));
  }

  return lines.join("\r\n");
}

function toCsvCell(value) {
  const text = String(value);
  if (/[",\r\n]/.test(text)) {
    return `"${text.replace(/"/g, '""')}"`;
  }

  return text;
}

function buildCsvFileName() {
  const baseName = state.sourceFileName
    ? state.sourceFileName.replace(/\.[^.]*$/, "")
    : "raw_records";
  const safeBaseName = baseName.replace(/[<>:"/\\|?*\x00-\x1F]/g, "_");
  return `${safeBaseName}_table.csv`;
}

function updatePager(total) {
  const lastPage = getLastPage();
  elements.prevPageButton.disabled = state.page <= 0;
  elements.nextPageButton.disabled = state.page >= lastPage;
  elements.pageInfo.textContent = total
    ? `${state.page + 1} / ${lastPage + 1} (${total} 件)`
    : "0 / 0 (0 件)";
}

function getLastPage() {
  return Math.max(0, Math.ceil(state.filteredIndexes.length / state.pageSize) - 1);
}

function clearAll() {
  state.records = [];
  state.columns = [...BASE_COLUMNS];
  state.filteredIndexes = [];
  state.columnFilters = new Map();
  state.page = 0;
  state.selected = null;
  state.sourceFileName = "";
  elements.fileInput.value = "";
  elements.tableHead.textContent = "";
  elements.tableBody.textContent = "";
  elements.window1Log.textContent = "";
  elements.window2Log.textContent = "";
  elements.clearButton.disabled = true;
  elements.copySelectedButton.disabled = true;
  elements.exportCsvButton.disabled = true;
  updatePager(0);
  setStatus("ファイル未読込");
  setCopyStatus("");
}

function normalizeFilter(value) {
  return String(value).toLocaleLowerCase("ja-JP");
}

function setStatus(message) {
  elements.statusText.textContent = message;
}

function setCopyStatus(message) {
  elements.copyStatusText.textContent = message;
}
