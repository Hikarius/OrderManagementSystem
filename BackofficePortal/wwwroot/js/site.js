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
                // Detect product vs order vs notifications shape
                if(arr.length && (arr[0].name !== undefined || arr[0].price !== undefined)){
                    var html = '<table class="table table-sm table-striped" id="productsTable"><thead><tr><th>Id</th><th>Name</th><th>Price</th><th>Stock</th></tr></thead><tbody>';
                    arr.forEach(function(p){
                        html += '<tr data-id="'+ (p.id||'') +'"><td>'+ (p.id||'') +'</td><td>'+ (p.name||'') +'</td><td>'+ (p.price||'') +'</td><td>'+ (p.stock||'') +'</td></tr>';
                    });

    // Select product row to fill form for update/delete
    $(document).on('click', '#productsTable tbody tr', function(){
        $('#productsTable tbody tr').removeClass('table-active');
        $(this).addClass('table-active');
        var id = $(this).data('id') || '';
        $('#selectedProductId').val(id);
        var tds = $(this).children('td');
        $('input[name="name"]').val($(tds[1]).text());
        $('input[name="price"]').val($(tds[2]).text());
        $('input[name="stock"]').val($(tds[3]).text());
    });

    $(document).on('click', '#btnAddProduct', function(){
        var body = {
            name: $('input[name="name"]').val(),
            description: $('input[name="description"]').val(),
            price: parseFloat($('input[name="price"]').val()||'0'),
            stock: parseInt($('input[name="stock"]').val()||'0',10),
            isActive: true
        };
        $.ajax({ url: '/Home/AddProduct', type: 'POST', data: JSON.stringify(body), contentType: 'application/json' })
          .done(function(res){ alert(res.success? 'Added: '+res.id : 'Failed'); $('#btnLoadProducts').trigger('click'); })
          .fail(function(xhr){ alert('Error '+xhr.status+': '+(xhr.responseText||xhr.statusText)); });
    });

    $(document).on('click', '#btnUpdateProduct', function(){
        var id = $('#selectedProductId').val(); if(!id){ alert('Select a product'); return; }
        var body = {
            id: id,
            name: $('input[name="name"]').val(),
            description: $('input[name="description"]').val(),
            price: parseFloat($('input[name="price"]').val()||'0'),
            stock: parseInt($('input[name="stock"]').val()||'0',10),
            isActive: true
        };
        $.ajax({ url: '/Home/UpdateProduct', type: 'PUT', data: JSON.stringify(body), contentType: 'application/json' })
          .done(function(res){ alert(res.success? 'Updated: '+res.id : 'Failed'); $('#btnLoadProducts').trigger('click'); })
          .fail(function(xhr){ alert('Error '+xhr.status+': '+(xhr.responseText||xhr.statusText)); });
    });

    $(document).on('click', '#btnDeleteProduct', function(){
        var id = $('#selectedProductId').val(); if(!id){ alert('Select a product'); return; }
        $.ajax({ url: '/Home/DeleteProduct?id='+encodeURIComponent(id), type: 'DELETE' })
          .done(function(res){ alert(res.success? 'Deleted: '+res.id : 'Failed'); $('#btnLoadProducts').trigger('click'); })
          .fail(function(xhr){ alert('Error '+xhr.status+': '+(xhr.responseText||xhr.statusText)); });
    });

    // Select order row
    $(document).on('click', '#ordersTable tbody tr', function(){
        $('#ordersTable tbody tr').removeClass('table-active');
        $(this).addClass('table-active');
        var id = $(this).data('id') || '';
        $('#selectedOrderId').val(id);
        if(id){
            $('#orderDetail').text('Loading detail...');
            var url = '/Home/GetOrderById?id=' + encodeURIComponent(id) + '&_ts=' + Date.now();
            $.get(url).done(function(data){
                try{
                    var o = (data && data.value) ? data.value : data;
                    if(!o){ $('#orderDetail').text('No detail'); return; }
                    var items = Array.isArray(o.items) ? o.items : [];
                    var html = '<div class="card card-body"><div class="row">'
                             + '<div class="col-md-4"><strong>Customer:</strong> '+ (o.customerEmail||'') +'</div>'
                             + '<div class="col-md-3"><strong>Total:</strong> '+ (o.totalPrice||'') +'</div>'
                             + '<div class="col-md-3"><strong>Status:</strong> '+ (typeof o.status==='number'? ['Pending','Confirmed','Cancelled'][o.status] : (o.status||'')) +'</div>'
                             + '</div>'
                             + '<div class="mt-2"><strong>Items</strong>'
                             + '<table class="table table-sm table-bordered mt-1"><thead><tr><th>Product</th><th>Qty</th><th>Unit</th><th>Total</th></tr></thead><tbody>';
                    items.forEach(function(it){
                        html += '<tr><td>'+ (it.productName||'') +'</td><td>'+ (it.quantity||0) +'</td><td>'+ (it.unitPrice||0) +'</td><td>'+ (it.totalPrice||0) +'</td></tr>';
                    });
                    html += '</tbody></table></div></div>';
                    $('#orderDetail').html(html);
                }catch(ex){ $('#orderDetail').text('Render error: '+ex); }
            }).fail(function(xhr){
                $('#orderDetail').text('Error ' + xhr.status + ' ' + (xhr.responseText||xhr.statusText));
            });
        } else {
            $('#orderDetail').empty();
        }
    });

    // Cancel order
    $(document).on('click', '#btnCancelOrder', function(){
        var id = $('#selectedOrderId').val();
        if(!id){ alert('Önce bir sipariş seçin.'); return; }
        $('#cancelResult').text('Cancelling...');
        $.ajax({
            url: '/Home/CancelOrder',
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(id)
        }).done(function(){
            $('#cancelResult').text('Cancelled');
            // refresh orders
            $('#btnLoadOrders').trigger('click');
        }).fail(function(xhr){
            $('#cancelResult').text('Error ' + xhr.status + ' ' + (xhr.responseText||xhr.statusText));
        });
    });
                    html += '</tbody></table>';
                    $(target).html(html);
                    return;
                }
                // notifications: array of { orderId, message }
                if(arr.length && (arr[0].message !== undefined) && (arr[0].orderId !== undefined)){
                    var htmlN = '<table class="table table-sm table-striped"><thead><tr><th>Order Id</th><th>Message</th></tr></thead><tbody>';
                    arr.forEach(function(n){
                        htmlN += '<tr><td>'+ (n.orderId||'') +'</td><td>'+ (n.message||'') +'</td></tr>';
                    });
                    htmlN += '</tbody></table>';
                    $(target).html(htmlN);
                    return;
                }
                if(arr.length && (arr[0].customerEmail !== undefined || arr[0].totalPrice !== undefined)){
                    var html2 = '<table class="table table-sm table-striped" id="ordersTable">'
                              + '<thead><tr><th>Id</th><th>Customer</th><th>Total</th><th>Status</th></tr></thead><tbody>';
                    arr.forEach(function(o){
                        var total = (o.totalPrice !== undefined && o.totalPrice !== null) ? o.totalPrice : '';
                        var st = (o.status !== undefined && o.status !== null) ? o.status : '';
                        if (typeof st === 'number') {
                            var map = ['Pending','Confirmed','Cancelled'];
                            st = map[st] !== undefined ? map[st] : st;
                        }
                        var id = (o.id||'');
                        html2 += '<tr data-id="'+ id +'">'
                              + '<td>'+ id +'</td>'
                              + '<td>'+ (o.customerEmail||'') +'</td>'
                              + '<td>'+ total +'</td>'
                              + '<td>'+ st +'</td>'
                              + '</tr>';
                    });
                    html2 += '</tbody></table>';
                    $(target).html(html2);
                    $('#orderActions').show();
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
