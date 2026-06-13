// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
$(function(){
    $(document).on('click', 'a[href^="/Home/Get"]', function(e){
        e.preventDefault();
        var url = $(this).attr('href');
        var target = '#'+ url.split('/').pop() + 'Content';
        $.get(url).done(function(data){
            $(target).text(JSON.stringify(data, null, 2));
        }).fail(function(){
            $(target).text('Error');
        });
    });
});
