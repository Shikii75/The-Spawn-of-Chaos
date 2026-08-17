mergeInto(LibraryManager.library, {
    ChaosNoise: function (time, x, y) {
        var t = time;
        var px = x;
        var py = y;
        // Layered sine hash — lightweight chaos noise for title distortion
        var n1 = Math.sin(px * 12.9898 + py * 78.233 + t * 4.17) * 43758.5453;
        n1 = n1 - Math.floor(n1);
        var n2 = Math.sin(px * 93.989 + py * 47.631 + t * 2.83) * 23421.631;
        n2 = n2 - Math.floor(n2);
        var n3 = Math.sin(px * 41.17 + py * 19.83 + t * 6.11) * 91823.17;
        n3 = n3 - Math.floor(n3);
        return (n1 + n2 + n3) / 3.0;
    },

    GlitchIntensity: function (time) {
        var t = time;
        var wave = 0.5 + 0.5 * Math.sin(t * 7.3);
        var spike = Math.pow(Math.abs(Math.sin(t * 23.7)), 8.0);
        return wave * 0.4 + spike * 0.6;
    }
});
