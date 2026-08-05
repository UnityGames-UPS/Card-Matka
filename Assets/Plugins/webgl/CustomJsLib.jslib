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

    // Outbound: Unity -> iframe host, as { type, data } via window.parent.postMessage.
    SendPostMessage: function (messagePtr) {
      var message = UTF8ToString(messagePtr);
      if (typeof window !== "undefined" && window.parent && typeof window.parent.postMessage === "function") {
        window.parent.postMessage({ type: message, data: {} }, "*");
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
    },

    RegisterVisibilityChangeListener: function(gameObjectNamePtr) {
      var gameObjectName = UTF8ToString(gameObjectNamePtr);

      function setUnityAudioSuspended(suspended) {
          try {
              var wa = (typeof WEBAudio !== 'undefined') ? WEBAudio
                     : (typeof Module !== 'undefined' && Module.WEBAudio) ? Module.WEBAudio
                     : null;
              if (!wa || !wa.audioContext) return;
              if (suspended) {
                  if (wa.audioContext.state === 'running') wa.audioContext.suspend();
              } else {
                  if (wa.audioContext.state === 'suspended') wa.audioContext.resume();
              }
          } catch (err) { console.warn('[JS] Unity audio suspend/resume failed:', err); }
      }

      function sendFocusToUnity(focused) {
          setUnityAudioSuspended(!focused);
          try {
              var value = focused ? '1' : '0';
              if (typeof SendMessage === 'function') {
                  SendMessage(gameObjectName, 'OnFocusChanged', value);
              } else if (typeof unityInstance !== 'undefined' && unityInstance && unityInstance.SendMessage) {
                  unityInstance.SendMessage(gameObjectName, 'OnFocusChanged', value);
              }
          } catch (err) {
              console.error('[JS] Error sending focus message to Unity:', err);
          }
      }

      window._unityVisibilityCallback = function() {
          var hidden = document.hidden || document.webkitHidden;
          sendFocusToUnity(!hidden);
      };
      window._unityWindowBlurCallback  = function() { sendFocusToUnity(false); };
      window._unityWindowFocusCallback = function() { sendFocusToUnity(true); };

      document.removeEventListener('visibilitychange',       window._unityVisibilityCallback);
      document.removeEventListener('webkitvisibilitychange', window._unityVisibilityCallback);
      window.removeEventListener('blur',  window._unityWindowBlurCallback);
      window.removeEventListener('focus', window._unityWindowFocusCallback);

      document.addEventListener('visibilitychange',       window._unityVisibilityCallback);
      document.addEventListener('webkitvisibilitychange', window._unityVisibilityCallback);
      window.addEventListener('blur',  window._unityWindowBlurCallback);
      window.addEventListener('focus', window._unityWindowFocusCallback);
    },

    // Self-contained resize bridge: the Unity page listens to its own viewport and pushes
    // "width,height" into Unity (<OC_GO>.<OC_METHOD>) — no dependency on the iframe host.
    RegisterResizeListener: function (gameObjectNamePtr, methodNamePtr) {
      var gameObjectName = UTF8ToString(gameObjectNamePtr);
      var methodName = UTF8ToString(methodNamePtr);

      function sendDimensionsToUnity() {
        try {
          // visualViewport is the accurate visible area on iOS; fall back to innerWidth/Height.
          var vv = window.visualViewport;
          var w = Math.round(vv ? vv.width : window.innerWidth);
          var h = Math.round(vv ? vv.height : window.innerHeight);
          var dimensions = w + ',' + h;
          if (typeof SendMessage === 'function') {
            SendMessage(gameObjectName, methodName, dimensions);
          } else if (typeof unityInstance !== 'undefined' && unityInstance && unityInstance.SendMessage) {
            unityInstance.SendMessage(gameObjectName, methodName, dimensions);
          }
        } catch (err) {
          console.error('[JS] resize send failed:', err);
        }
      }

      // No debounce here — the orientation receiver coalesces via StopCoroutine + waitForRotation,
      // so send on every event and let C# settle it. Remove any prior listener before re-adding.
      if (window._unityResizeCallback) {
        window.removeEventListener('resize', window._unityResizeCallback);
        window.removeEventListener('orientationchange', window._unityResizeCallback);
        if (window.visualViewport) window.visualViewport.removeEventListener('resize', window._unityResizeCallback);
      }
      window._unityResizeCallback = sendDimensionsToUnity;
      window.addEventListener('resize', window._unityResizeCallback);
      window.addEventListener('orientationchange', window._unityResizeCallback);
      if (window.visualViewport) window.visualViewport.addEventListener('resize', window._unityResizeCallback);

      sendDimensionsToUnity();   // initial sync
    },

    // Inbound auth: host posts { type:"TokenReceived", data:{cookie,socketURL,nameSpace} } -> Unity.
    RegisterTokenListener: function (gameObjectNamePtr, methodNamePtr) {
      var gameObjectName = UTF8ToString(gameObjectNamePtr);
      var methodName = UTF8ToString(methodNamePtr);

      if (window._unityTokenCallback) {
        window.removeEventListener('message', window._unityTokenCallback);
      }
      window._unityTokenCallback = function (event) {
        if (!event.data || event.data.type !== 'TokenReceived') return;
        var json = JSON.stringify(event.data.data);
        if (typeof SendMessage === 'function') {
          SendMessage(gameObjectName, methodName, json);
        } else if (typeof unityInstance !== 'undefined' && unityInstance && unityInstance.SendMessage) {
          unityInstance.SendMessage(gameObjectName, methodName, json);
        }
      };
      window.addEventListener('message', window._unityTokenCallback);
    }
});
