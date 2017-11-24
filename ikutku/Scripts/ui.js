var clearpixels = clearpixels || {};
clearpixels.ui = {};

window["clearpixels"] = clearpixels;

clearpixels.ui.getAvailableHeight = function(targetid) {
    var total = $(window).height();
    var offset = $(targetid).offset().top;
    var others = 0;
    for (var i = 1; i < arguments.length; i++) {
        others += $(arguments[i]).outerHeight();
    }
    console.log([total, offset, others].join(' '));
    return total - offset - others;
};