/**
 * Blob yardimcilari — PDF uclari Authorization basligi gerektirdigi icin
 * dogrudan `href` verilemez; blob cekilip object URL uzerinden acilir/indirilir.
 */

/** Blob'u dosya olarak indirir. */
export function downloadBlob(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = fileName;
  a.click();
  URL.revokeObjectURL(url);
}

/**
 * Blob'u yeni sekmede acar (PDF onizleme). Popup engellenirse indirmeye duser.
 * Object URL, sekmenin yuklemesine firsat vermek icin gecikmeli birakilir.
 */
export function openBlobInNewTab(blob: Blob, fallbackFileName: string): void {
  const url = URL.createObjectURL(blob);
  const opened = window.open(url, '_blank');
  if (!opened) {
    const a = document.createElement('a');
    a.href = url;
    a.download = fallbackFileName;
    a.click();
  }
  setTimeout(() => URL.revokeObjectURL(url), 60_000);
}
