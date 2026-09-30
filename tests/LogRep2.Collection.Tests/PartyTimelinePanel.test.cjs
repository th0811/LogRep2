const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const source = fs.readFileSync(path.join(__dirname, '../../src/LogRep2.App/Analyzer/PartyTimelineHtmlExporter.cs'), 'utf8');
const script = source.match(/private const string Script = """([\s\S]*?)""";/)[1];
const sortScript = source.match(/private const string SortScript = """([\s\S]*?)""";/)[1];

test('数値の桁区切りと小数を正しく比較し欠損を末尾に保ち文字列でも切り替える', () => {
    let click;
    const rows = [
        ['Carol', '1,000.25'], ['Boro', '9.50'], ['Alice', '—'], ['David', '0'], ['Erin', '9.50'],
    ].map(values => ({ cells: values.map(textContent => ({ textContent })) }));
    const body = { rows: [...rows], append(row) { this.rows = this.rows.filter(item => item !== row); this.rows.push(row); } };
    const headers = [0, 1].map(() => ({
        direction: 'none', getAttribute() { return this.direction; }, setAttribute(name, value) { this.direction = value; },
    }));
    const table = { tBodies: [body], querySelectorAll() { return headers; } };
    vm.runInNewContext(sortScript, { document: { addEventListener(name, callback) { click = callback; } } });
    const sort = column => click({ target: { closest() {
        return { dataset: { sort: String(column), type: column === 0 ? 'text' : 'number' },
            closest(selector) { return selector === 'table' ? table : headers[column]; } };
    } } });
    const names = () => body.rows.map(row => row.cells[0].textContent);
    sort(1);
    assert.deepEqual(names(), ['David', 'Boro', 'Erin', 'Carol', 'Alice']);
    assert.equal(headers[1].direction, 'ascending');
    sort(1);
    assert.deepEqual(names(), ['Carol', 'Boro', 'Erin', 'David', 'Alice']);
    assert.equal(headers[1].direction, 'descending');
    sort(0);
    assert.deepEqual(names(), ['Alice', 'Boro', 'Carol', 'David', 'Erin']);
    assert.equal(headers[1].direction, 'none');
    sort(0);
    assert.deepEqual(names(), ['Erin', 'David', 'Carol', 'Boro', 'Alice']);
});

test('詳細本文を文字列として表示し閉じるとページと図表の位置を復元する', () => {
    const handlers = {};
    const log = 'Aliceのバーサク！\n<script>危険な文字列</script>\n';
    const text = { textContent: '' };
    let opened = false;
    let focused = false;
    const timeline = { scrollLeft: 80, scrollTop: 900 };
    const card = { dataset: { log: 'event-1' }, focus(options) { focused = options.preventScroll; } };
    const dialog = {
        showModal() { opened = true; },
        addEventListener(name, callback) { handlers[name] = callback; },
    };
    const elements = { 'log-dialog': dialog, 'log-text': text, 'event-1': { content: { textContent: log } } };
    const window = { scrollX: 0, scrollY: 400, scrollTo(x, y) { this.scrollX = x; this.scrollY = y; } };
    vm.runInNewContext(script, {
        window,
        document: {
            getElementById(id) { return elements[id]; },
            querySelector() { return timeline; },
            addEventListener(name, callback) { handlers[name] = callback; },
        },
    });
    handlers.click({ target: { closest() { return null; } } });
    assert.equal(opened, false);
    handlers.click({ target: { closest() { return card; } } });
    assert.equal(opened, true);
    assert.equal(text.textContent, log);
    timeline.scrollTop = 0;
    timeline.scrollLeft = 0;
    window.scrollY = 0;
    handlers.close();
    assert.equal(timeline.scrollTop, 900);
    assert.equal(timeline.scrollLeft, 80);
    assert.equal(window.scrollY, 400);
    assert.equal(focused, true);
    assert.equal(text.textContent, '');
});
