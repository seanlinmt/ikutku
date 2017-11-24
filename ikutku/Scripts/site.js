var ikutku = ikutku || {};

ikutku.FollowerUnfollow = function(target, ids, ctx, successCallback) {
    var counter = parseInt($('.counter', ctx).text(), 10);

    $(target).ajax({
        url: '/api/followers',
        cache: false,
        type: 'DELETE',
        contentType: 'application/json; charset=utf-8',
        data: JSON.stringify(ids),
        success: function (result) {
            var data = result.response;
            var err = result.error;
            
            for (var i = 0; i < data.length; i++) {
                $(".selected[alt2='" + data[i] + "']", ctx).remove();
            }
            $('.counter', ctx).text(counter - data.length);
            
            var followingCount = parseInt($('#following_count').text(), 10);
            $('#following_count').text(followingCount - data.length);
            
            if (err != "") {
                $.jGrowl(err);
            } else {
                successCallback();
            }
        }
    });
};

ikutku.FollowerFollow = function(target, ids, ctx, successCallback) {
    var counter = parseInt($('.counter', ctx).text(), 10);

    $(target).ajax({
        url: '/api/followers',
        cache: false,
        type: 'POST',
        contentType: 'application/json; charset=utf-8',
        data: JSON.stringify(ids),
        success: function (result) {
            var data = result.response;
            var err = result.error;
            
            for (var i = 0; i < data.length; i++) {
                $(".selected[alt2='" + data[i] + "']", ctx).remove();
            }
            $('.counter', ctx).text(counter - data.length);
            
            var followingCount = parseInt($('#following_count').text(), 10);
            $('#following_count').text(followingCount + data.length);

            if (err != "") {
                $.jGrowl(err);
            } else {
                successCallback();
            }
        }
    });
};

jQuery.ajaxSettings.traditional = true;

$.fn.extend({
    buttonVisible: function (context) {
        if ($('.selected',context).length == 0) {
            $(this).hide();
        } else {
            $(this).show();
        }
    },
    showLoading: function () {
        //if ($(this).is('button')) {
            $(this).append("<span class='ajaxin'><img src='/Content/img/loading_grey.gif' /></span>");
        //}
        $(this).attr("disabled", true);
    },
    endLoading: function () {
        $('.ajaxin', this).remove();
        $(this).attr("disabled", false);
    },
    isProtected: function () {
        return ($(this).hasClass('user_overlay') || ($('.tweetDate', this).text().indexOf('protected') != -1));
    },
    quotaUpdate: function (result) {
        if (result == null || result.indexOf('-1') != -1) {
            return;
        }
        $(this).html(result);
    },
    showLoadingBlock: function (clear) {
        return this.each(function () {
            if ($('.loader', this).length == 0) {
                if (clear != undefined && clear) {
                    $(this).html('');
                }
                $(this).append("<div class='loader mt10 p10 clear' style='min-height:200px'><img src='/Content/img/loading_content.gif' /></div>");
            }
        });
    },
    getx: function (url, data, callback, type) {
        if (jQuery.isFunction(data)) {
            type = type || callback;
            callback = data;
            data = undefined;
        }

        return jQuery.ajax({
            type: 'get',
            url: url,
            data: data,
            success: callback,
            dataType: type,
            context: this.length == 0 ? undefined : this
        });
    },
    post: function (url, data, callback, type) {
        if (jQuery.isFunction(data)) {
            type = type || callback;
            callback = data;
            data = undefined;
        }

        return jQuery.ajax({
            type: 'post',
            url: url,
            data: data,
            success: callback,
            dataType: type,
            context: this.length == 0 ? undefined : this
        });
    },
    ajax: function (options) {
        options.context = this.length == 0 ? undefined : this;
        return jQuery.ajax(options);
    }
});

$(document).ajaxSend(function (event, request, settings) {
    if (settings.context != undefined &&
        settings.context.length &&
        settings.context[0].nodeType) {
        
        $(settings.context).showLoading();
        
        var ctx = $(settings.context).closest('.button_container');
        $('.error', ctx).html('').parent().hide();
    }
});

$(document).ajaxComplete(function (event, request, settings) {
    if (settings.context != undefined &&
        settings.context.length &&
        settings.context[0].nodeType) {
        
        $(settings.context).endLoading();
    }
});

$(document).ajaxError(function (event, request, settings) {
    
    var findErrorContainer = function (ctx) {
        var foundEntries = $(ctx).nextAll('.error_container');
        if (foundEntries.length != 0) {
            return foundEntries[0];
        } else {
            return $('.error_container', document);
        }
    };
    
    var errormsg = '';
    var redirectToLogin = false;
    switch (request.status) {
        case 400:
            errormsg = 'Bad request';
            break;
        case 401:
            errormsg = 'You are not signed in. Redirecting to sign in page ...';
            redirectToLogin = true;
            break;
        case 404:
            errormsg = 'Action not found';
            break;
        case 403:
            errormsg = "You do not have enough permissions to request this resource";
            break;
        case 408:
            // browser will retransmit if this is returned
            break;
        default:
            break;
    }

    if (errormsg == '') {
        errormsg = request.statusText;
    }

    if (settings.context != undefined &&
        settings.context.length &&
        settings.context[0].nodeType) {

        var container = findErrorContainer(settings.context);
        if (container.length != 0) {
            $('.error', container).html(errormsg);
            $(container).show();
        }
    } else {
        $('.loader').html('<p class="error_container"><span class="error"></span></p>');
        $('.error','.loader').html(errormsg).parent().show();
    }

    if (redirectToLogin) {
        window.location = '/';
    }    
});

