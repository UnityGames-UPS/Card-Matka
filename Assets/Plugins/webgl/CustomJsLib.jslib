mergeInto(LibraryManager.library, {

    // ─────────────────────────────────────────────────────────────────────────
    // BACKGROUND-RUN ENGINE
    //
    // Problem: browsers throttle / freeze WebGL tabs when backgrounded because
    // they suspend requestAnimationFrame (rAF). Unity's WebGL player is driven
    // entirely by rAF, so the whole engine — coroutines, DOTween, animations —
    // stops. Web Locks keep the JS *event loop* alive but do NOT keep rAF
    // firing; that requires a separate trick.
    //
    // Solution (three complementary layers):
    //
    //   Layer 1 — Web Worker setInterval
    //     A tiny inline Worker runs setInterval at 16 ms (≈60 fps) inside a
    //     dedicated thread. Workers are NEVER throttled by browsers regardless
    //     of tab visibility. Each tick posts a message back to the main thread.
    //
    //   Layer 2 — MessageChannel pump
    //     The main thread receives the Worker message and dispatches a
    //     MessageChannel port message. MessageChannel tasks fire at "task"
    //     priority — critically, they keep firing in background tabs even when
    //     rAF is paused. We use this to call _emscripten_set_main_loop_timing
    //     in setTimeout mode (mode 0) so Unity's main loop runs independently
    //     of the browser's rAF throttle.
    //
    //   Layer 3 — Web Lock (belt-and-suspenders)
    //     navigator.locks.request holds an exclusive lock for the session.
    //     Some browsers use lock presence as a hint to keep the tab alive.
    //
    // Together, the Worker drives a non-throttleable heartbeat that re-arms
    // Unity's main loop every ~16 ms, keeping coroutines, DOTween, and all
    // animations running at full speed in a background tab.
    // ─────────────────────────────────────────────────────────────────────────

    _bgWorker: null,
    _bgChannel: null,
    _bgLockHeld: false,
    _bgLockResolver: null,

    StartBackgroundRun: function () {
        if (Module._bgWorker !== null) return;

        var workerScript = [
            'self.onmessage = function(e) {',
            '  if (e.data === "start") {',
            '    self.setInterval(function() { self.postMessage("tick"); }, 16);',
            '  }',
            '};'
        ].join('\n');

        var blob = new Blob([workerScript], { type: 'application/javascript' });
        var workerUrl = URL.createObjectURL(blob);
        Module._bgWorker = new Worker(workerUrl);
        URL.revokeObjectURL(workerUrl);

        Module._bgChannel = new MessageChannel();

        Module._bgChannel.port2.onmessage = function () {
            if (typeof _emscripten_set_main_loop_timing === 'function') {
                _emscripten_set_main_loop_timing(0, 0);
            }
        };

        Module._bgChannel.port1.start();
        Module._bgChannel.port2.start();

        Module._bgWorker.onmessage = function () {
            Module._bgChannel.port1.postMessage('tick');
        };

        Module._bgWorker.postMessage('start');
        console.log('[BGRun] Web Worker + MessageChannel pump started.');

        if (typeof navigator !== 'undefined' && navigator.locks) {
            Module._bgLockHeld = true;
            navigator.locks.request(
                'unity-background-run',
                { mode: 'exclusive' },
                function () {
                    return new Promise(function (resolve) {
                        Module._bgLockResolver = resolve;
                        console.log('[BGRun] Web Lock acquired.');
                    });
                }
            ).catch(function (err) {
                console.warn('[BGRun] Web Lock request failed:', err);
                Module._bgLockHeld = false;
            });
        } else {
            console.warn('[BGRun] Web Locks API not available — Worker pump is the sole mechanism.');
        }
    },

    StopBackgroundRun: function () {
        if (Module._bgWorker !== null) {
            Module._bgWorker.terminate();
            Module._bgWorker = null;
            console.log('[BGRun] Worker terminated.');
        }
        if (Module._bgChannel !== null) {
            Module._bgChannel.port1.close();
            Module._bgChannel.port2.close();
            Module._bgChannel = null;
        }
        if (Module._bgLockResolver !== null) {
            Module._bgLockResolver();
            Module._bgLockResolver = null;
            Module._bgLockHeld = false;
            console.log('[BGRun] Web Lock released.');
        }
    },

    SendLogToReactNative: function (messagePtr) {
        var message = UTF8ToString(messagePtr);
        if (window.ReactNativeWebView) {
          window.ReactNativeWebView.postMessage(message);
        } 
    },

    SendPostMessage: function(messagePtr) {
      var message = UTF8ToString(messagePtr);
      if(window.ReactNativeWebView){
        if(message == "authToken"){
          window.ReactNativeWebView.postMessage("if message is authtoken");
          var injectedObjectJson = window.ReactNativeWebView.injectedObjectJson();
          var injectedObj = JSON.parse(injectedObjectJson);
          window.ReactNativeWebView.postMessage('Injected obj : ' + injectedObjectJson);
          var combinedData = JSON.stringify({
              socketURL: injectedObj.socketURL.trim(),
              cookie: injectedObj.token.trim(),
              nameSpace: injectedObj.nameSpace ? injectedObj.nameSpace.trim() : ""
          });
          if (typeof SendMessage === 'function') {
            SendMessage('SocketManager', 'ReceiveAuthToken', combinedData);
          }
        }
        window.ReactNativeWebView.postMessage(message);
      }
      else if(window.parent){
        if(window.parent.dispatchReactUnityEvent){
          console.log("Inside window parent");
          window.parent.dispatchReactUnityEvent(message); 
        }
      }
    },

    RequestFullscreen: function () {
      console.log('[JS] RequestFullscreen called');
      var el = document.documentElement;
      var req = el.requestFullscreen
             || el.webkitRequestFullscreen
             || el.mozRequestFullScreen
             || el.msRequestFullscreen;
      if (req) {
        req.call(el).then(function() {
          console.log('[JS] Fullscreen request succeeded');
        }).catch(function(err) {
          console.warn('[JS] RequestFullscreen failed:', err);
        });
      } else {
        console.error('[JS] No fullscreen API available!');
      }
    },

    ExitFullscreen: function () {
      console.log('[JS] ExitFullscreen called');
      var exit = document.exitFullscreen
              || document.webkitExitFullscreen
              || document.mozCancelFullScreen
              || document.msExitFullscreen;
      if (exit) {
        exit.call(document).then(function() {
          console.log('[JS] Exit fullscreen succeeded');
        }).catch(function(err) {
          console.warn('[JS] ExitFullscreen failed:', err);
        });
      } else {
        console.error('[JS] No exit fullscreen API available!');
      }
    },

    RegisterFullscreenChangeListener: function(gameObjectNamePtr) {
        var gameObjectName = UTF8ToString(gameObjectNamePtr);
        console.log('[JS] RegisterFullscreenChangeListener called for GameObject:', gameObjectName);

        function isCurrentlyFullscreen() {
            return !!(document.fullscreenElement || 
                      document.webkitFullscreenElement || 
                      document.mozFullScreenElement || 
                      document.msFullscreenElement);
        }

        function getUnityInstance() {
            if (typeof window.unityInstance !== 'undefined' && window.unityInstance && window.unityInstance.SendMessage) return window.unityInstance;
            if (typeof window.gameInstance !== 'undefined' && window.gameInstance && window.gameInstance.SendMessage) return window.gameInstance;
            if (typeof Module !== 'undefined' && Module && Module.SendMessage) return Module;
            if (typeof unityInstance !== 'undefined' && unityInstance && unityInstance.SendMessage) return unityInstance;
            if (window.parent && window.parent !== window) {
                if (window.parent.unityInstance && window.parent.unityInstance.SendMessage) return window.parent.unityInstance;
                if (window.parent.gameInstance && window.parent.gameInstance.SendMessage) return window.parent.gameInstance;
            }
            for (var key in window) {
                try {
                    if (window.hasOwnProperty(key)) {
                        var obj = window[key];
                        if (obj && typeof obj === 'object' && typeof obj.SendMessage === 'function') return obj;
                    }
                } catch(e) {}
            }
            return null;
        }

        function sendToUnity(isFS) {
            try {
                var instance = getUnityInstance();
                if (instance && instance.SendMessage) {
                    instance.SendMessage(gameObjectName, 'OnFullscreenChanged', isFS ? '1' : '0');
                    console.log('[JS] Sent fullscreen state to Unity: ' + (isFS ? 'EXPANDED' : 'SHRINK'));
                } else {
                    console.warn('[JS] Unity instance not available, cannot send');
                }
            } catch (err) {
                console.error('[JS] Error sending message to Unity:', err);
            }
        }

        window._unityFullscreenCallback = function() {
            var isFS = isCurrentlyFullscreen();
            console.log('[JS] Fullscreen event fired. State:', isFS ? 'EXPANDED' : 'SHRINK');
            sendToUnity(isFS);
        };

        document.removeEventListener('fullscreenchange',       window._unityFullscreenCallback);
        document.removeEventListener('webkitfullscreenchange', window._unityFullscreenCallback);
        document.removeEventListener('mozfullscreenchange',    window._unityFullscreenCallback);
        document.removeEventListener('MSFullscreenChange',     window._unityFullscreenCallback);

        document.addEventListener('fullscreenchange',       window._unityFullscreenCallback);
        document.addEventListener('webkitfullscreenchange', window._unityFullscreenCallback);
        document.addEventListener('mozfullscreenchange',    window._unityFullscreenCallback);
        document.addEventListener('MSFullscreenChange',     window._unityFullscreenCallback);

        console.log('[JS] Fullscreen event listeners registered for:', gameObjectName);
    }
});
