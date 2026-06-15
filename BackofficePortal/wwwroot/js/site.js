// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
$(function(){
    function mapTarget(url, $link){
        // honor explicit data-target first
        var explicit = $link && $link.attr('data-target');
        if(explicit) return explicit;
        var action = url.split('/').pop(); // e.g. GetProducts
        var name = (action || '').replace(/^Get/i, '').toLowerCase();
        if(name === 'products') return '#catalogContent';
        if(name === 'notifications') return '#notificationContent';
        if(name) return '#' + name + 'Content';
        return '#ordersContent';
    }

    function renderData(target, data){
        try {
            var arr = Array.isArray(data) ? data : (data && Array.isArray(data.value) ? data.value : null);
            if(arr){
                // Detect product vs order shape
                if(arr.length && (arr[0].name !== undefined || arr[0].price !== undefined)){
                    var html = '<table class="table table-sm table-striped"><thead><tr><th>Name</th><th>Price</th><th>Stock</th></tr></thead><tbody>';
                    arr.forEach(function(p){
                        html += '<tr><td>'+ (p.name||'') +'</td><td>'+ (p.price||'') +'</td><td>'+ (p.stock||'') +'</td></tr>';
                    });
                    html += '</tbody></table>';
                    $(target).html(html);
                    return;
                }
                if(arr.length && (arr[0].customerEmail !== undefined || arr[0].totalPrice !== undefined)){
                    var html2 = '<table class="table table-sm table-striped"><thead><tr><th>Customer</th><th>Total</th><th>Status</th></tr></thead><tbody>';
                    arr.forEach(function(o){
                        html2 += '<tr><td>'+ (o.customerEmail||'') +'</td><td>'+ (o.totalPrice||'') +'</td><td>'+ (o.status||'') +'</td></tr>';
                    });
                    html2 += '</tbody></table>';
                    $(target).html(html2);
                    return;
                }
            }
            // Fallback to pretty JSON
            $(target).text(JSON.stringify(data, null, 2));
        } catch(ex){
            $(target).text('Render error: ' + ex);
        }
    }

    $(document).on('click', 'a[href^="/Home/Get"]', function(e){
        e.preventDefault();
        var url = $(this).attr('href');
        // prevent caching issues
        var sep = url.indexOf('?') === -1 ? '?' : '&';
        var fullUrl = url + sep + '_ts=' + Date.now();
        var target = mapTarget(url, $(this));
        var $t = $(target);
        $t.text('Loading...');
        $.get(fullUrl)
            .done(function(data){
                renderData($t, data);
            })
            .fail(function(xhr){
                var msg = 'Error ' + xhr.status + ' ' + (xhr.statusText||'') + ' ' + (xhr.responseText||'');
                $t.text(msg);
            });
    });

    // Dynamic order items handling
    function addItemRow(products){
        var opts = (products||[]).map(function(p){ return '<option value="'+p.id+'">'+p.name+' ('+p.stock+')</option>'; }).join('');
        var row = '<tr>'+
            '<td><select class="form-select product-select">'+ opts +'</select></td>'+
            '<td><input type="number" class="form-control qty-input" min="1" value="1" /></td>'+
            '<td><button type="button" class="btn btn-sm btn-outline-danger btnRemoveItem">Remove</button></td>'+
            '</tr>';
        $('#orderItemsTable tbody').append(row);
    }

    var cachedProducts = null;
    function ensureProducts(cb){
        if(cachedProducts){ cb(cachedProducts); return; }
        // load products once for selection
        $.get('/Home/GetProducts').done(function(data){
            var arr = Array.isArray(data) ? data : (data && Array.isArray(data.value) ? data.value : []);
            cachedProducts = arr || [];
            cb(cachedProducts);
        }).fail(function(){ cb([]); });
    }

    $(document).on('click', '#btnAddItem', function(){
        ensureProducts(function(products){ addItemRow(products); });
    });

    $(document).on('click', '.btnRemoveItem', function(){
        $(this).closest('tr').remove();
    });

    $(document).on('click', '#btnSubmitOrder', function(){
        var email = $('input[name="customerEmail"]').val();
        var tel = $('input[name="customerTelNumber"]').val();
        var items = [];
        $('#orderItemsTable tbody tr').each(function(){
            var pid = $(this).find('select.product-select').val();
            var qty = parseInt($(this).find('input.qty-input').val(),10) || 0;
            if(pid && qty > 0){ items.push({ productId: pid, quantity: qty }); }
        });

        if(!email || items.length === 0){
            alert('Email ve en az bir ürün seçiniz.');
            return;
        }

        var payload = { customerEmail: email, customerTelNumber: tel, items: items };
        $.ajax({
            url: '/Home/AddOrder',
            type: 'POST',
            data: JSON.stringify(payload),
            contentType: 'application/json'
        }).done(function(res){
            if(res && res.success){
                alert('Order created: ' + res.orderId);
                $('#ordersContent').text('');
            } else {
                alert('Failed: ' + (res && res.error ? res.error : 'Unknown'));
            }
        }).fail(function(xhr){
            alert('Error ' + xhr.status + ': ' + (xhr.responseText || xhr.statusText));
        });
    });
});
