// Uygulamanın "yüklenebilir" sayılması için bir fetch işleyicisi gerekir.
// Bilerek ÖNBELLEK YOK: panel canlı veri gösteriyor ve bayat JS/veri servis etmek
// yanlış karar aldırabilir. İstek doğrudan ağa gider.
self.addEventListener('install', (event) => {
  event.waitUntil(self.skipWaiting());
});

self.addEventListener('activate', (event) => {
  event.waitUntil(self.clients.claim());
});

self.addEventListener('fetch', (event) => {
  event.respondWith(fetch(event.request));
});
