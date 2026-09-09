window.appJs = {
  printSlip: (html) => {
    const w = window.open('', '_blank', 'width=980,height=800');
    if (!w) { alert('Vui lòng cho phép popup để in phiếu.'); return; }
    w.document.write(`<!DOCTYPE html><html lang="vi"><head><meta charset="utf-8"><title>In Phiếu Giao Dịch - AITS Quản Lý Kho</title>
      <style>
        @import url('https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700;800&display=swap');
        @page { size: A4 portrait; margin: 10mm 12mm; }
        * {
          box-sizing: border-box;
          -webkit-print-color-adjust: exact !important;
          print-color-adjust: exact !important;
          color-adjust: exact !important;
        }
        html, body {
          font-family: 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
          background: #ffffff !important;
          color: #0f172a !important;
          margin: 0;
          padding: 0;
          width: 100%;
        }
        .printable-page {
          max-width: 190mm;
          margin: 0 auto;
          padding: 10px;
        }
        @media print {
          body { padding: 0 !important; }
          .printable-page { width: 100% !important; max-width: 100% !important; padding: 0 !important; }
        }
        table { width: 100%; border-collapse: collapse; }
      </style></head><body><div class="printable-page">${html}</div></body></html>`);
    w.document.close();
    w.focus();
    setTimeout(() => { w.print(); w.close(); }, 400);
  },
  downloadCsv: (filename, content) => {
    const blob = new Blob(['\uFEFF' + content], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    setTimeout(() => { document.body.removeChild(a); URL.revokeObjectURL(url); }, 0);
  },
  confirm: (message) => window.confirm(message),
};
